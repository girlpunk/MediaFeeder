export function initPlayer(DotNetHelper, videoId)
{
    "use strict";
    console.log("Starting player init");

    return new YT.Player(
        'ytplayer',
        {
            height: '100%',
            width: '100%',
            videoId: videoId,
            playerVars: {
                origin: window.location,
                autoplay: 1,
            },
            events: {
                'onReady': (event => DotNetHelper.invokeMethodAsync('OnPlayerReady', event)),
                'onStateChange': (event => DotNetHelper.invokeMethodAsync('OnPlayerStateChange', event)),
                'onPlaybackQualityChange': (event => DotNetHelper.invokeMethodAsync('OnPlaybackQualityChange', event)),
                'onPlaybackRateChange': (event => DotNetHelper.invokeMethodAsync('OnPlaybackRateChange', event)),
                'onError': (event => DotNetHelper.invokeMethodAsync('OnError', event)),
                'onApiChange': (event => DotNetHelper.invokeMethodAsync('OnApiChange', event)),
            }
        });
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
