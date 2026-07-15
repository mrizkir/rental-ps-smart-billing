// TODO: sesuaikan per lokasi — IP LAN PC kasir + port Flask (default 5001)
const BILLING_SERVER_URL = 'http://192.168.100.x:5001';
// TODO: samakan dengan SmartTvs.Id di database untuk TV unit ini
const TV_ID = 1;

var warningHideTimer = null;

function initTVWindow() {
    tizen.systeminfo.getPropertyValue('VIDEOSOURCE', function (videoSource) {
        var connected = videoSource.connected || [];
        var hdmiSource = null;

        for (var i = 0; i < connected.length; i++) {
            if (connected[i].type === 'HDMI') {
                hdmiSource = connected[i];
                break;
            }
        }

        if (!hdmiSource) {
            console.log('No HDMI source found in connected video sources');
            return;
        }

        tizen.tvwindow.setSource(
            hdmiSource,
            function () {
                console.log('TVWindow source set to HDMI');
                tizen.tvwindow.show(
                    function () {
                        console.log('TVWindow shown fullscreen');
                    },
                    function (error) {
                        console.log('TVWindow show error: ' + error.message);
                    },
                    ['0px', '0px', '100%', '100%'],
                    'MAIN'
                );
            },
            function (error) {
                console.log('TVWindow setSource error: ' + error.message);
            }
        );
    }, function (error) {
        console.log('VIDEOSOURCE error: ' + error.message);
    });
}

function checkSignal() {
    try {
        var source = tizen.tvwindow.getSource();
        var idleOverlay = document.getElementById('idle-overlay');

        if (!idleOverlay || source.signal === null || typeof source.signal === 'undefined') {
            return;
        }

        if (source.signal === false) {
            idleOverlay.classList.add('show');
        } else if (source.signal === true) {
            idleOverlay.classList.remove('show');
        }
    } catch (error) {
        console.log('checkSignal error: ' + error.message);
    }
}

function showWarningBanner(message) {
    var banner = document.getElementById('warning-banner');
    if (!banner) {
        return;
    }

    banner.textContent = message || 'Waktu hampir habis';
    banner.classList.add('show');

    if (warningHideTimer) {
        clearTimeout(warningHideTimer);
    }

    warningHideTimer = setTimeout(function () {
        banner.classList.remove('show');
        warningHideTimer = null;
    }, 5000);
}

async function checkBillingStatus() {
    try {
        var url = BILLING_SERVER_URL + '/api/tv-notification?tv_id=' + encodeURIComponent(TV_ID);
        var response = await fetch(url);
        if (!response.ok) {
            return;
        }

        var data = await response.json();
        if (data && data.show_warning === true) {
            showWarningBanner(data.message);
        }
    } catch (error) {
        // Network errors must not disrupt checkSignal / TVWindow loop
    }
}

var init = function () {
    console.log('init() called');

    initTVWindow();
    setInterval(checkSignal, 2000);
    setInterval(checkBillingStatus, 3000);

    document.addEventListener('visibilitychange', function () {
        if (document.hidden) {
            // Something you want to do when hide or exit.
        } else {
            // Something you want to do when resume.
            initTVWindow();
        }
    });

    // add eventListener for keydown (template Back / RETURN handler)
    document.addEventListener('keydown', function (e) {
        switch (e.keyCode) {
        case 37: //LEFT arrow
            break;
        case 38: //UP arrow
            break;
        case 39: //RIGHT arrow
            break;
        case 40: //DOWN arrow
            break;
        case 13: //OK button
            break;
        case 10009: //RETURN button
            tizen.application.getCurrentApplication().exit();
            break;
        default:
            console.log('Key code : ' + e.keyCode);
            break;
        }
    });
};

window.onload = init;
