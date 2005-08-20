/* $Id$ */

/***
  This file is part of avahi.
 
  avahi is free software; you can redistribute it and/or modify it
  under the terms of the GNU Lesser General Public License as
  published by the Free Software Foundation; either version 2.1 of the
  License, or (at your option) any later version.
 
  avahi is distributed in the hope that it will be useful, but WITHOUT
  ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
  or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General
  Public License for more details.
 
  You should have received a copy of the GNU Lesser General Public
  License along with avahi; if not, write to the Free Software
  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
  USA.
***/

#ifdef HAVE_CONFIG_H
#include <config.h>
#endif

#include <stdio.h>
#include <assert.h>
#include <stdlib.h>
#include <time.h>

#include <avahi-core/core.h>
#include <avahi-common/simple-watch.h>
#include <avahi-common/malloc.h>
#include <avahi-common/error.h>

static AvahiSimplePoll *simple_poll = NULL;

static void resolve_callback(
    AvahiSServiceResolver *r,
    AvahiIfIndex interface,
    AvahiProtocol protocol,
    AvahiResolverEvent event,
    const char *name,
    const char *type,
    const char *domain,
    const char *host_name,
    const AvahiAddress *address,
    uint16_t port,
    AvahiStringList *txt,
    void* userdata) {
    
    assert(r);

    /* Called whenever a service has been resolved successfully or timed out */

    if (event == AVAHI_RESOLVER_TIMEOUT)
        fprintf(stderr, "Failed to resolve service '%s' of type '%s' in domain '%s'.\n", name, type, domain);
    else {
        char a[128], *t;

        assert(event == AVAHI_RESOLVER_FOUND);
        
        fprintf(stderr, "Service '%s' of type '%s' in domain '%s':\n", name, type, domain);

        avahi_address_snprint(a, sizeof(a), address);
        t = avahi_string_list_to_string(txt);
        fprintf(stderr, "\t%s:%u (%s) TXT=%s\n", host_name, port, a, t);
        avahi_free(t);
    }

    avahi_s_service_resolver_free(r);
}

static void browse_callback(
    AvahiSServiceBrowser *b,
    AvahiIfIndex interface,
    AvahiProtocol protocol,
    AvahiBrowserEvent event,
    const char *name,
    const char *type,
    const char *domain,
    void* userdata) {
    
    AvahiServer *s = userdata;
    assert(b);

    /* Called whenever a new services becomes available on the LAN or is removed from the LAN */

    fprintf(stderr, "%s: service '%s' of type '%s' in domain '%s'\n",
            event == AVAHI_BROWSER_NEW ? "NEW" : "REMOVED",
            name,
            type,
            domain);
    
    /* If it's new, let's resolve it */
    if (event == AVAHI_BROWSER_NEW)
        
        /* We ignore the returned resolver object. In the callback function
        we free it. If the server is terminated before the callback
        function is called the server will free the resolver for us. */

        if (!(avahi_s_service_resolver_new(s, interface, protocol, name, type, domain, AVAHI_PROTO_UNSPEC, resolve_callback, s)))
            fprintf(stderr, "Failed to resolve service '%s': %s\n", name, avahi_strerror(avahi_server_errno(s)));
}

int main(int argc, char*argv[]) {
    AvahiServerConfig config;
    AvahiServer *server = NULL;
    AvahiSServiceBrowser *sb;
    int error;
    int ret = 1;

    /* Initialize the psuedo-RNG */
    srand(time(NULL));

    /* Allocate main loop object */
    if (!(simple_poll = avahi_simple_poll_new())) {
        fprintf(stderr, "Failed to create simple poll object.\n");
        goto fail;
    }

    /* Do not publish any local records */
    avahi_server_config_init(&config);
    config.publish_hinfo = 0;
    config.publish_addresses = 0;
    config.publish_workstation = 0;
    config.publish_domain = 0;
    
    /* Allocate a new server */
    server = avahi_server_new(avahi_simple_poll_get(simple_poll), &config, NULL, NULL, &error);

    /* Free the configuration data */
    avahi_server_config_free(&config);

    /* Check wether creating the server object succeeded */
    if (!server) {
        fprintf(stderr, "Failed to create server: %s\n", avahi_strerror(error));
        goto fail;
    }
    
    /* Create the service browser */
    if (!(sb = avahi_s_service_browser_new(server, AVAHI_IF_UNSPEC, AVAHI_PROTO_UNSPEC, "_http._tcp", NULL, browse_callback, server))) {
        fprintf(stderr, "Failed to create service browser: %s\n", avahi_strerror(avahi_server_errno(server)));
        goto fail;
    }
    
    /* Run the main loop */
    for (;;)
        if (avahi_simple_poll_iterate(simple_poll, -1) != 0)
            break;
    
    ret = 0;
    
fail:
    
    /* Cleanup things */
    if (sb)
        avahi_s_service_browser_free(sb);
    
    if (server)
        avahi_server_free(server);

    if (simple_poll)
        avahi_simple_poll_free(simple_poll);

    return ret;
}