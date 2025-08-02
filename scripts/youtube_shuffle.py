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
    content_id: str = None


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
            print(f"Received finished event: {status.content_id}")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=True, content_id=status.content_id))

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
            print("Received mark as watched and skip command")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=True))

        elif rep.ShouldSkip:
            print("Received skip command")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=False))

        elif rep.NextVideoId > 0:
            print(f"Received next video ID: {rep.NextVideoId}")
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
    global listenerMedia, status_message_queue, executor, session_reader, cast

    if session_reader:
        session_reader.cancel()

    print("Connecting to server...")
    session_iterator = stub.PlaybackSession(message_queue_iterator(status_message_queue))
    session_reader = executor.submit(listenerMedia.on_ses_rep, session_iterator)
    status_message_queue.put(Api_pb2.PlaybackSessionRequest(Title = cast.name))

def UpdateCast():
    global cast
    try:
        cast.media_controller.update_status()
        return True
    except pychromecast.error.NotConnected:
        print("Waiting for chromecast...")
        time.sleep(3)
        return False

try:
    current_video_id = None
    current_content_id = None
    while True:
        if not session_reader or not session_reader.running():
            ConnectToServer()

        if current_video_id and not UpdateCast():
            continue

        event = listenerMedia.get_event(5)
        if event and event.next_video_id:
            current_video_id = event.next_video_id
            id_response = stub.Video(Api_pb2.VideoRequest(Id=current_video_id))
            current_content_id = id_response.VideoId

            print(f"Playing {current_video_id}: {id_response.Title} [{current_content_id}]")
            status_message_queue.put(Api_pb2.PlaybackSessionRequest(VideoId = current_video_id))

            # if the chromecast has been used for other apps, cast lib might incorrectly think it is already launched.
            # this makes sure it reconnects and is ready to receive play commands.  hopefully.
            yt.update_screen_id()
            yt.start_session_if_none()
            time.sleep(1)

            yt.clear_playlist()
            yt.play_video(current_content_id)

        elif event and event.go_next:
            # this check is to protected against the chromecast sending multiple finished events.
            if not event.content_id or event.content_id == current_content_id:
                if current_video_id and event.mark_watched:
                    print(f"Marking {current_video_id} as watched...")
                    stub.Watched(Api_pb2.WatchedRequest(Id=current_video_id, Watched=True))

                current_video_id = None
                current_content_id = None
                print("Requesting next video...")
                status_message_queue.put(Api_pb2.PlaybackSessionRequest(Action = Api_pb2.POP_NEXT_VIDEO))
                time.sleep(1)  # let chromecast finish before trying to play next video.
            else:
                print(f"Ignoring event: {event}")
except KeyboardInterrupt:
    print("Shuting down...")

# Shut down discovery
browser.stop_discovery()

print("fin.")
status_message_queue.put(Api_pb2.PlaybackSessionRequest(EndSession = True))

executor.shutdown(wait=True, cancel_futures=True)
sys.exit(0)
