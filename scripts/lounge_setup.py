#!/usr/bin/env python

"""Setup and token retrieval for Youtube TV App's 'Lounge' API.."""

import asyncio
import logging

import pyytlounge

from auth import MediaFeederConfig


async def _main() -> None:
    """Main entrypoint, start playback."""

    async with pyytlounge.YtLoungeApi("MediaFeeder") as api:
        pairing_code = input("Enter pairing code: ")
        paired_and_linked = await api.pair(pairing_code)
        if not paired_and_linked:
            print("Could not pair")

        connected = await api.connect()
        if not connected:
            print("Could not connect")

        name = input("Enter display name: ")

        config = MediaFeederConfig()
        config.save_player(name, api.auth.serialize())

if __name__ == "__main__":
    asyncio.run(_main(), debug=True)
