#!/usr/bin/env python3
"""Import watch history from YouTube via a 'Google Takeout' Export."""

import argparse
import json
import logging
import sys
import urllib.parse
from datetime import datetime

import grpc
from pathlib import Path

import Api_pb2
import Api_pb2_grpc
import auth
import common

common.set_logging()
logger = logging.getLogger("history-import")

# Enable deprecation warnings etc.
if not sys.warnoptions:
    import warnings

    warnings.simplefilter("default")

parser = argparse.ArgumentParser(
    description="Example on how to use the Youtube Controller.",
)
parser.add_argument(
    "--server",
    help="MediaFeeder server address and port",
    required=True,
)
parser.add_argument("--history", help="YouTube watch history JSON file", required=True)
args = parser.parse_args()

with Path(args.history).open() as f:
    history = json.load(f)
if not history[0]["titleUrl"]:
    logger.error("Incorrect format: %s", args.history)
    sys.exit(1)

logger.info("History entries: %s", len(history))

config = auth.MediaFeederConfig()
bearer_credentials = grpc.metadata_call_credentials(common.AuthGateway(config))
ssl_credentials = grpc.ssl_channel_credentials()
composite_credentials = grpc.composite_channel_credentials(
    ssl_credentials,
    bearer_credentials,
)

channel_options = [
    ("grpc.keepalive_time_ms", 8000),
    ("grpc.keepalive_timeout_ms", 5000),
    ("grpc.http2.max_pings_without_data", 0),
    ("grpc.keepalive_permit_without_calls", 1),
]

channel = grpc.secure_channel(
    args.server,
    composite_credentials,
    options=channel_options,
)
stub = Api_pb2_grpc.APIStub(channel)

history.reverse()
entries_read = 0
entries_matched = 0
entries_modified = 0
for entry in history:
    entries_read += 1
    if entries_read % 500 == 0:
        logger.info("Read %s entries, matched %s, modified %s", entries_read, entries_matched, entries_modified)

    raw_url = entry.get("titleUrl")
    if not raw_url:
        if entry.get("title") in ["Viewed Ads On YouTube Homepage", "Answered survey question"]:
            continue
        logger.warning("Unknown entry: %s", entry)
        continue

    url = urllib.parse.urlparse(raw_url)
    if "/post/" in url.path:
        continue

    query = urllib.parse.parse_qs(url.query)
    if "v" not in query:
        if url.netloc == "www.google.com":
            continue
        logger.warning("Unknown URL: %s", raw_url)
        continue
    video_id = query["v"][0]

    when_seconds = int(datetime.fromisoformat(entry.get("time")).timestamp())

    search = stub.Search(
        Api_pb2.SearchRequest(Provider="Youtube", ProviderVideoId=video_id),
    )
    if len(search.Videos) == 0:
        continue
    elif len(search.Videos) > 1:
        logger.warning("Multiple matches for %s: %s", video_id, [v.VideoId for v in search.Videos])
        continue
    entries_matched += 1

    found = search.Videos[0]
    if not found.Watched or found.WatchedWhenSeconds < when_seconds:
        stub.Watched(Api_pb2.WatchedRequest(Id=found.VideoId, Watched=True, WhenSeconds=when_seconds))
        entries_modified += 1
