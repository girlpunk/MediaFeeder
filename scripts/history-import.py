#!/usr/bin/env python3
# pylint: disable=invalid-name

import Api_pb2
import Api_pb2_grpc
import argparse
import grpc
import json
import sys
import urllib.parse


# Enable deprecation warnings etc.
if not sys.warnoptions:
    import warnings
    warnings.simplefilter("default")

parser = argparse.ArgumentParser(description="Example on how to use the Youtube Controller.")
parser.add_argument("--server",     help='MediaFeeder server address and port', required=True)
parser.add_argument("--token",      help='Authentication token', required=True)
parser.add_argument("--history",    help='YouTube watch history JSON file', required=True)
args = parser.parse_args()

with open(args.history) as f:
    history = json.load(f)
if not history[0]['titleUrl']:
    print(f"Incorrect format: {args.history}")
    sys.exit(1)
print(f"History entries: {len(history)}")

bearer_credentials = grpc.access_token_call_credentials(args.token)
ssl_credentials = grpc.ssl_channel_credentials()
composite_credentials = grpc.composite_channel_credentials(ssl_credentials, bearer_credentials)

channel_options = [
    ("grpc.keepalive_time_ms", 8000),
    ("grpc.keepalive_timeout_ms", 5000),
    ("grpc.http2.max_pings_without_data", 0),
    ("grpc.keepalive_permit_without_calls", 1),
]

channel = grpc.secure_channel(args.server, composite_credentials, options=channel_options)
stub = Api_pb2_grpc.APIStub(channel)

history.reverse()
entries_read = 0
entries_matched = 0
for entry in history:
    entries_read += 1
    if entries_read % 100 == 0:
        print(f"Read {entries_read} entries, matched {entries_matched}")

    rawUrl = entry.get('titleUrl')
    if not rawUrl:
        print(f"Unknown entry: {entry}")
        continue

    url = urllib.parse.urlparse(rawUrl)
    if "/post/" in url.path:
        continue

    query = urllib.parse.parse_qs(url.query)
    if not 'v' in query:
        print(f"Unknown URL: {rawUrl}")
        continue
    video_id = query['v'][0]

    search = stub.Search(Api_pb2.SearchRequest(Provider="Youtube", ProviderVideoId=video_id))
    if len(search.VideoId) == 0:
        continue
    entries_matched += 1
    stub.Watched(Api_pb2.WatchedRequest(Id=search.VideoId[0], Watched=True))
