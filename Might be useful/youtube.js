let player;
let dotNetHelper;

export function initPlayer(DotNetHelper, videoId) {
    console.log("Starting player init");
    dotNetHelper = DotNetHelper;
    player = new YT.Player('ytplayer', {
        height: '100%',
        width: '100%',
        videoId: videoId,
        playerVars: {
            origin: window.location,
            autoplay: 1,
        },
        events: {
            'onReady': onPlayerReady,
            'onStateChange': onPlayerStateChange,
            'onPlaybackQualityChange': onPlaybackQualityChange,
            'onPlaybackRateChange': onPlaybackRateChange,
            'onError': onError,
            'onApiChange': onApiChange
        }
    });
}

function onPlayerReady(event) {
    dotNetHelper.invokeMethodAsync('onPlayerReady', event);
    console.log("Player Ready: ", event);
    event.target.playVideo();
}

function onPlaybackQualityChange(event) {
    dotNetHelper.invokeMethodAsync('onPlaybackQualityChange', event);
    console.log("Quality change: ", event);
}

function onPlaybackRateChange(event) {
    dotNetHelper.invokeMethodAsync('onPlaybackRateChange', event);
    console.log("Playback rate change: ", event);
}

function onError(event) {
    dotNetHelper.invokeMethodAsync('onError', event);
    console.log("Playback Error: ", event);
    if(event.data == 150) {
        // "This error is the same as 101. It's just a 101 error in disguise!" - from the YT API Documentation, not 100% this is true.
        // Skip to the next video after 10 seconds, do not mark as watched.
        setTimeout(goNextVideo, 10 * 1000);
    }
}

function onApiChange(event) {
    dotNetHelper.invokeMethodAsync('onApiChange', event);
    console.log("API Change: ", event);
}

function onPlayerStateChange(event) {
    dotNetHelper.invokeMethodAsync('onPlayerStateChange', event);
    console.log("State change: ", event);
    if (event.data == YT.PlayerState.ENDED) {
        console.log("Video finished!");
        setWatchedStatus(1);
    } else if (event.data == YT.PlayerState.UNSTARTED) {
        player.playVideo();
    }
}

// function isFullScreen() {
//     return (window.fullScreen) || (window.innerWidth == screen.width && window.innerHeight == screen.height);
// }

/*$(window).on('resize', function() {
    var container = $("#ytplayer");
    var parent = container.parent();

    if (isFullScreen()) {
        container.css("position", "absolute");
        container.css("top", "0");
        container.css("left", "0");

        container.css("width", "100vw");
        container.css("height", "100vh");

        parent.css("position", "absolute");
        parent.css("top", "0");
        parent.css("left", "0");

        parent.css("z-index", "1");
    } else {
        // fullscreen is cancelled
        container.css("position", "static");
        container.css("width", "100%");
        container.css("height", "80vh");
        parent.css("position", "relative");
    }
});*/

/*
var evt = document.createEvent("HTMLEvents");
evt.initEvent("resize", false, true);
window.dispatchEvent(evt);*/
