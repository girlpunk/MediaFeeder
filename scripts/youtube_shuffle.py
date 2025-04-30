#!/usr/bin/env python
# pylint: disable=invalid-name

import argparse
import sys
from threading import Event
from queue import Queue

import pychromecast
from pychromecast.controllers.youtube import YouTubeController

import grpc

from pychromecast.controllers.media import MediaStatus, MediaStatusListener
import Api_pb2
import Api_pb2_grpc

class MyMediaStatusListener(MediaStatusListener):
    """Status media listener"""

    def __init__(self, name: str | None, cast: pychromecast.Chromecast, queue: Queue) -> None:
        self.name = name
        self.cast = cast
        self.queue = queue
        self.last_is_idle = Event()

    def new_media_status(self, status: MediaStatus) -> None:
        #print("status media change:")
        #print(status)

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self.last_is_idle.set()
        else:
            self.last_is_idle.clear()

        # This is for adverts
        if not status.supports_pause:
            return

        status_message = Api_pb2.PlaybackSessionRequest(
            Duration = int(status.current_time),
            Provider = "youtube" if status.content_type == "x-youtube/video" else None,
            State = status.player_state,
            Volume = status.volume_level,
            Rate = status.playback_rate
        )

        self.queue.put(status_message)

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        print(
            "load media failed for queue item id: ",
            queue_item_id,
            " with code: ",
            error_code,
        )

def messageQueueIterator(queue: Queue):
    while True:
        yield queue.get()


# Enable deprecation warnings etc.
if not sys.warnoptions:
    import warnings

    warnings.simplefilter("default")

parser = argparse.ArgumentParser(description="Example on how to use the Youtube Controller.")
parser.add_argument("--server",     help='MediaFeeder server address and port', required=True)
parser.add_argument("--cast",       help='Name of cast device')
parser.add_argument("--known-host", help="Add known host (IP), can be used multiple times", action="append")
parser.add_argument("--folder",     help='Folder ID', required=True, type=int)
parser.add_argument("--token",      help='Authentication token', required=True)
args = parser.parse_args()

chromecasts, browser = pychromecast.get_listed_chromecasts(friendly_names=[args.cast], known_hosts=args.known_host)

if not chromecasts:
    print(f'No chromecast with name "{args.cast}" discovered')
    sys.exit(1)

cast = chromecasts[0]
# Start socket client's worker thread and wait for initial status update
cast.wait()

bearer_credentials = auth_creds = grpc.access_token_call_credentials(args.token)
ssl_credentials = grpc.ssl_channel_credentials()
composite_credentials = grpc.composite_channel_credentials(ssl_credentials, bearer_credentials)

channel_options = [
    ("grpc.keepalive_time_ms", 8000),
    ("grpc.keepalive_timeout_ms", 5000),
    ("grpc.http2.max_pings_without_data", 5),
    ("grpc.keepalive_permit_without_calls", 1),
]

channel = grpc.secure_channel(args.server, composite_credentials, options=channel_options)
stub = Api_pb2_grpc.APIStub(channel)

shuffleRequest = Api_pb2.ShuffleRequest(FolderId=args.folder)
videos = stub.Shuffle(shuffleRequest).Id

yt = YouTubeController()
cast.register_handler(yt)

status_message_queue = Queue()

listenerMedia = MyMediaStatusListener(cast.name, cast, status_message_queue)
cast.media_controller.register_status_listener(listenerMedia)

state_response_iterator = stub.PlaybackSession(messageQueueIterator(status_message_queue))

while len(videos) > 0:
    video = videos.pop(0)

    id_request = Api_pb2.VideoRequest(Id=video)
    id_response = stub.Video(id_request)

    print(f"Playing {id_response.Title} ({id_response.VideoId})")

    yt.play_video(id_response.VideoId)

    listenerMedia.last_is_idle.clear()
    listenerMedia.last_is_idle.wait()

    watched_request = Api_pb2.WatchedRequest(Id=video, Watched=True)
    stub.Watched(watched_request)


# Shut down discovery
browser.stop_discovery()
