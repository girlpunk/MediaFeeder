#!/usr/bin/env python3
# vim: tw=0 ts=4 sw=4

# need:
# - name
# - Channel or Playlist ID   UUx6cailiCkg_mlMM7JX5yfA
# - Channel ID               UCx6cailiCkg_mlMM7JX5yfA
# - Channel Name

import argparse
from typing import NamedTuple

import grpc
import requests
from bs4 import BeautifulSoup

import Api_pb2
import Api_pb2_grpc
import auth
import common

parser = argparse.ArgumentParser()
parser.add_argument("channelurl")
parser.add_argument("--server", help="MediaFeeder server address and port", required=True)
parser.add_argument("--folder", help="Folder ID", required=True, type=int)
args = parser.parse_args()
print(f"     channel url: {args.channelurl}")
print(f"       folder id: {args.folder}")


class ChanInfo(NamedTuple):
    channel_id: str = None
    pl_id: str = None
    name: str = None


def get_chan_info(youtube_channel_url):
    headers = {
        # curl seems to work, but browser agent gets a 302 and the wrong page.
        "User-Agent": "curl/8.14.1",
    }
    response = requests.get(youtube_channel_url, headers=headers)
    if response.status_code != 200:
        raise Exception(f"Failed to fetch page: {response.status_code}")

    soup = BeautifulSoup(response.text, "html.parser")

    meta_tag = soup.find("meta", attrs={"itemprop": "identifier"})
    if not meta_tag or not meta_tag.get("content"):
        raise Exception("Channel ID meta tag not found.")

    name_tag = soup.find("meta", attrs={"itemprop": "name"})
    if not name_tag or not name_tag.get("content"):
        raise Exception("Channel name meta tag not found.")

    channel_id = meta_tag["content"]
    name = name_tag["content"]

    if not channel_id.startswith("UC"):
        raise Exception(f"Invalid channel_id: {channel_id}")
    pl_id = "UU" + channel_id[2:]

    return ChanInfo(channel_id=channel_id, pl_id=pl_id, name=name)


chan_info = get_chan_info(args.channelurl)
print(f"      channel id: {chan_info.channel_id}")
print(f"           pl id: {chan_info.pl_id}")
print(f"            name: {chan_info.name}")


config = auth.MediaFeederConfig()
bearer_credentials = grpc.metadata_call_credentials(common._AuthGateway(config))
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

added = stub.AddSubscription(
    Api_pb2.AddSubscriptionRequest(
        Name=chan_info.name,
        ChannelId=chan_info.channel_id,
        PlaylistId=chan_info.pl_id,
        Provider="Youtube",
        FolderId=args.folder,
    ),
)

print(f"new subscription: {added.SubscriptionId}")
