#!/usr/bin/env python3
# pylint: disable=invalid-name

import argparse
import sys
import time
from threading import Event
from queue import Queue
from concurrent.futures import ThreadPoolExecutor
from datetime import timedelta

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
        self.reset()

    def reset(self):
        self.mark_watched = True
        self.last_is_idle.clear()
        self.last_status = None

    def wait(self):
        self.last_is_idle.wait()

    def new_media_status(self, status: MediaStatus) -> None:
        self.last_status = status
        #print(f"new_media_status: {status}")

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self.last_is_idle.set()
        else:
            self.last_is_idle.clear()

        # This is for adverts
        if not status.supports_pause:
            return

        status_message = Api_pb2.PlaybackSessionRequest()
        status_message.Duration = int(status.current_time)
        if status.content_type == "x-youtube/video":
            status_message.Provider = "Youtube"
        status_message.State = status.player_state
        status_message.Volume = int(status.volume_level * 100)
        status_message.Rate = status.playback_rate
        self.queue.put(status_message)

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        print(
            "load media failed for queue item id: ",
            queue_item_id,
            " with code: ",
            error_code,
        )

    def on_ses_rep(self, iterator):
        try:
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
            self.mark_watched = True
            self.last_is_idle.set()

        elif rep.ShouldSkip:
            self.mark_watched = False
            self.last_is_idle.set()


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
parser.add_argument("--duration",   help='Target duration in minutes', required=False, type=int)
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

shuffleRequest = Api_pb2.ShuffleRequest(FolderId=args.folder)
if args.duration:
    shuffleRequest.DurationMinutes = args.duration
videos = stub.Shuffle(shuffleRequest).Id

print("Videos to play:")
for video in videos:
    info = stub.Video(Api_pb2.VideoRequest(Id=video))
    dur = str(timedelta(seconds=info.Duration))
    print(f"  - {dur} {info.Title} ({info.VideoId})")

yt = YouTubeController()
cast.register_handler(yt)

status_message_queue = Queue()

listenerMedia = MyMediaStatusListener(cast.name, cast, status_message_queue)
cast.media_controller.register_status_listener(listenerMedia)

state_response_iterator = stub.PlaybackSession(message_queue_iterator(status_message_queue))
executor = ThreadPoolExecutor()
executor.submit(listenerMedia.on_ses_rep, state_response_iterator)


while len(videos) > 0:
    video = videos.pop(0)

    id_request = Api_pb2.VideoRequest(Id=video)
    id_response = stub.Video(id_request)

    print(f"Playing: {id_response.Title} ({id_response.VideoId})")

    yt.play_video(id_response.VideoId)
    listenerMedia.reset()

    status_message = Api_pb2.PlaybackSessionRequest()
    status_message.VideoId = video
    status_message_queue.put(status_message)

    listenerMedia.wait()

    if listenerMedia.mark_watched:
        watched_request = Api_pb2.WatchedRequest(Id=video, Watched=True)
        stub.Watched(watched_request)

    time.sleep(1)


# Shut down discovery
browser.stop_discovery()

print("fin.")
status_message_queue.put(Api_pb2.PlaybackSessionRequest(EndSession = True))

executor.shutdown(wait=True, cancel_futures=True)
sys.exit(0)
