import yt_dlp
from concurrent import futures
import grpc
import datetime
import glob
import logging

import downloadServer_pb2
import downloadServer_pb2_grpc

from grpc_health.v1 import health
from grpc_health.v1 import health_pb2
from grpc_health.v1 import health_pb2_grpc

YDL_DOWNLOAD_OPTS = {
    # 'progress_hooks': [my_hook],
    'format': "(bv[vcodec~='^(avc|h264)']+ba[acodec~='^(aac|mp?4a)']) / "
              "(bv[vcodec~='^(vp9)']+ba[acodec=opus])",
    'format_sort': ['res:1080'],
    'fragment_retries': 10,
    'ignoreerrors': 'only_download',
    'outtmpl': {'default': 'test'},
    'postprocessors': [
        {
            'key': 'SponsorBlock',
            'api': 'https://sponsor.ajay.app',
            'categories': {
                'chapter',
                'filler',
                'interaction',
                'intro',
                'music_offtopic',
                'outro',
                'poi_highlight',
                'preview',
                'selfpromo',
                'sponsor'
            },
            'when': 'after_filter'
        },
        {
            'key': 'FFmpegEmbedSubtitle',
            'already_have_subtitle': False
        },
        {
            'key': 'ModifyChapters',
            'force_keyframes': False,
            'remove_chapters_patterns': [],
            'remove_ranges': [],
            'remove_sponsor_segments': {
                'interaction',
                'selfpromo',
                'sponsor'
            },
            'sponsorblock_chapter_title': '[SponsorBlock]: %(category_names)l'
        },
        {
            'key': 'FFmpegMetadata',
            'add_chapters': True,
            'add_metadata': True,
        },
        {
            'key': 'EmbedThumbnail',
            'already_have_thumbnail': False
        }
    ],
    'retries': 10,
    'writeautomaticsub': True
}

YDL_OPTS = {
    'fragment_retries': 10,
    'ignoreerrors': 'only_download',
    'outtmpl': {'default': 'test', 'pl_thumbnail': ''},
    'retries': 10
}

class Downloader(downloadServer_pb2_grpc.YTDownloaderServicer):

    def About(self, request: downloadServer_pb2.AboutRequest, context: grpc.ServicerContext) -> downloadServer_pb2.AboutReply:
        with (yt_dlp.YoutubeDL(YDL_OPTS) as ydl):
            info = ydl.sanitize_info(ydl.extract_info(request.VideoUrl, download=False))

            response = downloadServer_pb2.AboutReply()
            response.AgeLimit = info["age_limit"]
            response.Category.extend(info["categories"])
            response.ChannelId = info["channel_id"]
            response.Description = info["description"]
            response.Duration = info["duration"]
            response.Embeddable = info["playable_in_embed"]
            response.FileSize = info["filesize_approx"]

            if "release_timestamp" in info and info["release_timestamp"] is not None:
                response.Released = info["release_timestamp"]

            if "release_date" in info:
                response.ReleaseDate = int(datetime.datetime.timestamp(datetime.datetime.strptime(info["release_date"], "%Y%M%d")))

            response.Tags.extend(info["tags"])
            response.ThumbnailUrl = info["thumbnail"]
            response.Timestamp = info["timestamp"]
            response.Title = info["title"]
            response.UploadDate = int(datetime.datetime.timestamp(datetime.datetime.strptime(info["upload_date"], "%Y%M%d")))
            response.Views = info["view_count"]

            match info["availability"]:
                case "private":
                    response.Availability = downloadServer_pb2.PRIVATE
                case "premium_only":
                    response.Availability = downloadServer_pb2.PREMIUM_ONLY
                case "subscriber_only":
                    response.Availability = downloadServer_pb2.SUBSCRIBER_ONLY
                case "needs_auth":
                    response.Availability = downloadServer_pb2.NEEDS_AUTH
                case "unlisted":
                    response.Availability = downloadServer_pb2.UNLISTED
                case "public":
                    response.Availability = downloadServer_pb2.PUBLIC

            match info["live_status"]:
                case "is_live":
                    response.LiveStatus = downloadServer_pb2.IS_LIVE
                case "is_upcoming":
                    response.LiveStatus = downloadServer_pb2.IS_UPCOMING
                case "was_live":
                    response.LiveStatus = downloadServer_pb2.WAS_LIVE
                case "not_live":
                    response.LiveStatus = downloadServer_pb2.NOT_LIVE
                case "post_live":
                    response.LiveStatus = downloadServer_pb2.POST_LIVE

            return response

    def Download(self, request: downloadServer_pb2.DownloadRequest, context: grpc.ServicerContext) -> downloadServer_pb2.DownloadReply:
        opts = YDL_DOWNLOAD_OPTS

        response = downloadServer_pb2.DownloadReply()
        response.Status = downloadServer_pb2.STARTING

        #def status_hook(d):
        #    if response.Filename != d["Filename"]:
        #        print(d["Filename"])
        #        response.Filename = d["filename"]
            #if "downloaded_bytes" in d and "total_bytes" in d and d["downloaded_bytes"] is not None and d["total_bytes"] is not None:
            #    response.Progress = float(d["downloaded_bytes"])/d["total_bytes"]

        #opts["progress_hooks"] = [status_hook]
        opts["outtmpl"] = {'default': request.OutputPath}

        status = -1

        #async def download():
        with (yt_dlp.YoutubeDL(YDL_DOWNLOAD_OPTS) as ydl):
            status = ydl.download(f"https://www.youtube.com/watch?v={request.VideoUrl}")

        #loop = asyncio.new_event_loop()
        #task = loop.create_task(download())
        #loop.run_forever()

        #while not task.done():
        #    yield response
        #    time.sleep(1)

        #loop.close()

        if status == 0:
            response.Status = downloadServer_pb2.DONE
            response.Filename = glob.glob(f"{glob.escape(request.OutputPath)}*")[0]
        else:
            response.Status = downloadServer_pb2.TEMPORARY_ERROR
            response.Exitcode = status

        #response.Progress = 1

        return response


if __name__ == '__main__':
    logging.basicConfig()
    bind_to = "0.0.0.0:30033"
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=2))
    downloadServer_pb2_grpc.add_YTDownloaderServicer_to_server(Downloader(), server)
    health_pb2_grpc.add_HealthServicer_to_server(health.HealthServicer(), server)

    server.add_insecure_port(bind_to)
    server.start()
    print(f"Listening on {bind_to}")
    server.wait_for_termination()
