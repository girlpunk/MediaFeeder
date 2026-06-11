#!/usr/bin/env python3
# vim: tw=0 ts=4 sw=4
"""Get provider ID for videos with stars in a given folder."""

import argparse
import asyncio
import sys

import Api_pb2
import common


class GetStars(common.MfClient):

    def __init__(self) -> None:
        super().__init__(verbose=True)

    async def get_stars(self, folder_id):
        search = await self._stub.Search(
            Api_pb2.SearchRequest(Provider="Youtube", FolderId=folder_id, Star=True)
        )
        for video in search.Videos:
            if not video.Star:
                raise Exception(
                    f"invalid search results: {video.ProviderVideoId} is not stared."
                )
        return search.Videos


async def _main() -> None:
    common.set_logging(sys.stderr)

    parser = argparse.ArgumentParser()
    parser.add_argument("--folder", help="Folder ID", required=True, type=int)
    args = parser.parse_args()

    async with GetStars() as gs:
        for v in await gs.get_stars(args.folder):
            print(f"{v.ProviderVideoId}")  # noqa: T201


if __name__ == "__main__":
    asyncio.run(_main())
