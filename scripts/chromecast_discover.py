#!/usr/bin/env python
"""Find available chromecasts."""

import logging
import sys
import time

import common
import pychromecast
import zeroconf

common.set_logging()

logger = logging.getLogger("youtube_play")


# Enable deprecation warnings etc.
if not sys.warnoptions:
    import warnings

    warnings.simplefilter("default")


zconf = zeroconf.Zeroconf()
browser = pychromecast.CastBrowser(
    pychromecast.SimpleCastListener(
        lambda uuid, service: print(browser.devices[uuid].friendly_name)
    ),
    zconf,
)
browser.start_discovery()

time.sleep(30)

# Shut down discovery
browser.stop_discovery()

logger.info("fin.")
