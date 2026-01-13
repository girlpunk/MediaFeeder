#!/usr/bin/env python3
# vim: tw=0 ts=4 sw=4
"""Get provider ID for videos with stars in a given folder."""

import argparse

import grpc

import Api_pb2
import Api_pb2_grpc
import auth
import common

parser = argparse.ArgumentParser()
parser.add_argument("--server", help="MediaFeeder server address and port", required=True)
parser.add_argument("--folder", help="Folder ID", required=True, type=int)
args = parser.parse_args()

config = auth.MediaFeederConfig()
bearer_credentials = grpc.metadata_call_credentials(common.AuthGateway(config))
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

search = stub.Search(Api_pb2.SearchRequest(Provider="Youtube", FolderId=args.folder, Star=True))
for video in search.Videos:
    if not video.Star:
        raise Exception(f"invalid search results: {video.ProviderVideoId} is not stared.")
    print(f"{video.ProviderVideoId}")
