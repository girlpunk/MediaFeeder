#!/usr/bin/env python

import asyncio
import logging
import traceback

import pyatv
import pyatv.storage.file_storage


def set_logging():
    logging.basicConfig(
        level=logging.DEBUG,
        format="%(asctime)s [%(levelname)s] %(message)s",
        handlers=[
            logging.FileHandler("atv.log"),
            #            logging.StreamHandler()
        ],
    )
    logging.getLogger("asyncio").setLevel(logging.DEBUG)


async def connect():
    try:
        loop = asyncio.get_event_loop()
        loop.set_exception_handler(error)

        storage = pyatv.storage.file_storage.FileStorage.default_storage(loop)
        await storage.load()

        atv = await pyatv.scan(loop, hosts=["10.101.15.6"])  # 192.168.164.103"])
        device = await pyatv.connect(atv[0], loop=loop, storage=storage)

        listener = MyPushListener(device)
        device.push_updater.listener = listener
        device.push_updater.start()

        # wait forever
        await asyncio.Event().wait()
    except Exception as e:
        print(traceback.format_exc())
        raise e
    finally:
        device.close()


class MyPushListener(pyatv.interface.PushListener):
    def __init__(self, device):
        try:
            self._device = device
        except Exception as e:
            print(traceback.format_exc())
            raise e

    def playstatus_update(self, updater, playstatus):
        asyncio.run_coroutine_threadsafe(self._update_playing(playstatus), asyncio.get_event_loop())

    def playstatus_error(self, updater, exception):
        # Error in exception
        print("an error happened!")
        print(exception)

    async def _update_playing(self, playing):
        try:
            print(f"Playing update: {playing}")
            print(playing)

            print(f"media_type: {playing.media_type.name}")
            print(f"device_state: {playing.device_state.name}")
            print(f"title: {playing.title}")
            print(f"artist: {playing.artist}")
            print(f"album: {playing.album}")
            print(f"genre: {playing.genre}")
            print(f"total_time: {playing.total_time}")
            print(f"position: {playing.position}")
            print(f"shuffle: {playing.shuffle.name}")
            print(f"repeat: {playing.repeat.name}")
            print(f"hash: {playing.hash}")
            print(f"series_name: {playing.series_name}")
            print(f"season_number: {playing.season_number}")
            print(f"episode_number: {playing.episode_number}")
            print(f"content_identifier: {playing.content_identifier}")
            print(f"itunes_store_identifier: {playing.itunes_store_identifier}")

        except Exception as e:
            print(traceback.format_exc())
            raise e


def error(loop, context):
    exception = context["exception"]
    print(f"got exception {exception}")


if __name__ == "__main__":
    set_logging()
    device = asyncio.run(connect(), debug=True)
