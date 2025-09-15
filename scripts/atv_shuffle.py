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
        ]
    )
    logging.getLogger("asyncio").setLevel(logging.DEBUG)

async def connect():
  try:
    loop = asyncio.get_event_loop()
    loop.set_exception_handler(error)

    storage = pyatv.storage.file_storage.FileStorage.default_storage(loop)
    await storage.load()

    atv = await pyatv.scan(loop, hosts=["10.101.15.6"]) # 192.168.164.103"])
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
        print("Playing update: {0}".format(playing))
        print(playing)

        print("media_type: {0}".format(playing.media_type.name))
        print("device_state: {0}".format(playing.device_state.name))
        print("title: {0}".format(playing.title))
        print("artist: {0}".format(playing.artist))
        print("album: {0}".format(playing.album))
        print("genre: {0}".format(playing.genre))
        print("total_time: {0}".format(playing.total_time))
        print("position: {0}".format(playing.position))
        print("shuffle: {0}".format(playing.shuffle.name))
        print("repeat: {0}".format(playing.repeat.name))
        print("hash: {0}".format(playing.hash))
        print("series_name: {0}".format(playing.series_name))
        print("season_number: {0}".format(playing.season_number))
        print("episode_number: {0}".format(playing.episode_number))
        print("content_identifier: {0}".format(playing.content_identifier))
        print("itunes_store_identifier: {0}".format(playing.itunes_store_identifier))


      except Exception as e:
        print(traceback.format_exc())
        raise e

def error(loop, context):
    exception = context["exception"]
    print("got exception {0}".format(exception))


if __name__ == "__main__":
    set_logging()
    device = asyncio.run(connect(), debug=True)
