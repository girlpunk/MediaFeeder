#!/usr/bin/env python

"""Setup and token retrieval for Youtube TV App's 'Lounge' API.."""

import asyncio
import logging

import pyytlounge

import common
from auth import MediaFeederConfig

common.set_logging()
logger = logging.getLogger("LoungeSetup")


async def _main() -> None:
    """Begin main entrypoint."""
    async with pyytlounge.YtLoungeApi("MediaFeeder") as api:
        pairing_code = input("Enter pairing code: ")
        paired_and_linked = await api.pair(pairing_code)
        if not paired_and_linked:
            logger.error("Could not pair")

        connected = await api.connect()
        if not connected:
            logger.error("Could not connect")

        name = input("Enter display name: ")

        config = MediaFeederConfig()
        config.save_player(name, api.auth.serialize())


if __name__ == "__main__":
    asyncio.run(_main(), debug=True)
