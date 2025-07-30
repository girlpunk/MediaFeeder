#!/usr/bin/env python3
# pylint: disable=invalid-name

import argparse
import sys
import queue
import time
from queue import Queue
from concurrent.futures import ThreadPoolExecutor
from datetime import timedelta
from typing import NamedTuple

import pychromecast
from pychromecast.controllers.youtube import YouTubeController

import grpc

from pychromecast.controllers.media import MediaStatus, MediaStatusListener
from api import Api_pb2
from api import Api_pb2_grpc


class QueueEvent(NamedTuple):
    next_video_id: int = None
    go_next: bool = False
    mark_watched: bool = False


class MyMediaStatusListener(MediaStatusListener):
    """Status media listener"""

    def __init__(self, name: str | None, cast: pychromecast.Chromecast, status_queue: Queue) -> None:
        self.name = name
        self.cast = cast
        self.status_queue = status_queue
        self.event_queue = Queue()
        self.last_status = None

    def get_event(self, timeout):
        try:
            return self.event_queue.get(timeout=timeout)
        except queue.Empty:
            return None

    def new_media_status(self, status: MediaStatus) -> None:
        self.last_status = status
        #print(f"new_media_status: {status}")

        # This is for adverts
        if not status.supports_pause:
            return

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            print(f"FINISHED: {status}")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=True))

        status_message = Api_pb2.PlaybackSessionRequest()
        status_message.Duration = int(status.current_time)
        if status.content_type == "x-youtube/video":
            status_message.Provider = "Youtube"
        status_message.State = status.player_state
        status_message.Volume = int(status.volume_level * 100)
        status_message.Rate = status.playback_rate
        self.status_queue.put(status_message)

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        print(
            "load media failed for queue item id: ",
            queue_item_id,
            " with code: ",
            error_code,
        )

    def on_ses_rep(self, iterator):
        try:
            print("Started streaming RPC.")
            for rep in iterator:
                try:
                    self.on_ses_rep_msg(rep)
                except Exception as e:
                    print(f"failed to handle msg {rep}: {e}")
        except Exception as e:
            print(f"failed to read stream: {e}")

    def on_ses_rep_msg(self, rep):
        if rep.ShouldPlayPause:
            if not self.last_status:
                print("can not play/pause without last_status.")
            elif self.last_status.player_is_paused:
                cast.media_controller.play()
                print("playing")
            elif self.last_status.player_is_playing:
                cast.media_controller.pause()
                print("paused")
            else:
                print(f"can not play/pause in state: {self.last_status.player_state}")

        elif rep.ShouldWatch:
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=True))

        elif rep.ShouldSkip:
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=False))

        elif rep.NextVideoId > 0:
            self.event_queue.put(QueueEvent(next_video_id=rep.NextVideoId))


def message_queue_iterator(queue: Queue):
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

yt = YouTubeController()
cast.register_handler(yt)

executor = ThreadPoolExecutor()
status_message_queue = Queue()

listenerMedia = MyMediaStatusListener(cast.name, cast, status_message_queue)
cast.media_controller.register_status_listener(listenerMedia)
session_reader = None

def ConnectToServer():
    global listenerMedia, status_message_queue, executor, session_reader

    if session_reader:
        session_reader.cancel()

    print("Connecting to server...")
    session_iterator = stub.PlaybackSession(message_queue_iterator(status_message_queue))
    session_reader = executor.submit(listenerMedia.on_ses_rep, session_iterator)

try:
    current_video_id = None
    while True:
        if not session_reader or not session_reader.running():
            ConnectToServer()

        event = listenerMedia.get_event(5)
        cast.media_controller.update_status()

        if event and event.next_video_id:
            current_video_id = event.next_video_id
            id_response = stub.Video(Api_pb2.VideoRequest(Id=current_video_id))
            print(f"Playing {current_video_id}: {id_response.Title} [{id_response.VideoId}]")
            status_message_queue.put(Api_pb2.PlaybackSessionRequest(VideoId = current_video_id))
            yt.play_video(id_response.VideoId)

        elif event and event.go_next:
            if current_video_id and event.mark_watched:
                print(f"Marking {current_video_id} as watched...")
                stub.Watched(Api_pb2.WatchedRequest(Id=current_video_id, Watched=True))
                current_video_id = None
            status_message_queue.put(Api_pb2.PlaybackSessionRequest(Action = Api_pb2.POP_NEXT_VIDEO))
except KeyboardInterrupt:
    print("shuting down...")

# Shut down discovery
browser.stop_discovery()

print("fin.")
status_message_queue.put(Api_pb2.PlaybackSessionRequest(EndSession = True))

executor.shutdown(wait=True, cancel_futures=True)
sys.exit(0)
