// TODO: sesuaikan per lokasi — IP LAN PC kasir + port Flask (default 5001)
const BILLING_SERVER_URL = 'http://192.168.100.5:5001';
// TODO: samakan dengan SmartTvs.Id di database untuk TV unit ini
const TV_ID = 1;

var warningHideTimer = null;
var clockTimer = null;
var sessionActive = false;
var sessionEndsAtMs = null;
var sessionOpenEnded = false;
var sessionPackageName = '';

var ID_WEEKDAYS = [
    'Minggu', 'Senin', 'Selasa', 'Rabu', 'Kamis', 'Jumat', 'Sabtu'
];
var ID_MONTHS = [
    'Januari', 'Februari', 'Maret', 'April', 'Mei', 'Juni',
    'Juli', 'Agustus', 'September', 'Oktober', 'November', 'Desember'
];

function pad2(n) {
    return n < 10 ? '0' + n : String(n);
}

function formatDuration(totalSeconds) {
    if (totalSeconds < 0) {
        totalSeconds = 0;
    }
    var h = Math.floor(totalSeconds / 3600);
    var m = Math.floor((totalSeconds % 3600) / 60);
    var s = totalSeconds % 60;
    return pad2(h) + ':' + pad2(m) + ':' + pad2(s);
}

function updateHomeClock() {
    var clockEl = document.getElementById('home-clock');
    var dateEl = document.getElementById('home-date');
    if (!clockEl && !dateEl) {
        return;
    }

    var now = new Date();
    if (clockEl) {
        clockEl.textContent =
            pad2(now.getHours()) + ':' +
            pad2(now.getMinutes()) + ':' +
            pad2(now.getSeconds());
    }
    if (dateEl) {
        dateEl.textContent =
            ID_WEEKDAYS[now.getDay()] + ', ' +
            now.getDate() + ' ' +
            ID_MONTHS[now.getMonth()] + ' ' +
            now.getFullYear();
    }
}

function setHomeVisible(visible) {
    var home = document.getElementById('home-screen');
    if (!home) {
        return;
    }
    if (visible) {
        home.classList.add('show');
    } else {
        home.classList.remove('show');
    }
}

function setSessionHudVisible(visible) {
    var hud = document.getElementById('session-hud');
    if (!hud) {
        return;
    }
    if (visible) {
        hud.classList.add('show');
    } else {
        hud.classList.remove('show');
        hud.classList.remove('warning');
    }
}

function updateSessionHudTick() {
    var packageEl = document.getElementById('session-package');
    var countdownEl = document.getElementById('session-countdown');
    var hud = document.getElementById('session-hud');
    if (!sessionActive) {
        setSessionHudVisible(false);
        return;
    }

    if (packageEl) {
        packageEl.textContent = sessionPackageName || 'Sesi aktif';
    }

    if (sessionOpenEnded) {
        if (countdownEl) {
            countdownEl.textContent = 'Free Play';
        }
        if (hud) {
            hud.classList.remove('warning');
        }
        setSessionHudVisible(true);
        return;
    }

    if (sessionEndsAtMs === null) {
        if (countdownEl) {
            countdownEl.textContent = '--:--:--';
        }
        setSessionHudVisible(true);
        return;
    }

    var remainingSec = Math.ceil((sessionEndsAtMs - Date.now()) / 1000);
    if (countdownEl) {
        countdownEl.textContent = formatDuration(remainingSec);
    }
    if (hud) {
        if (remainingSec <= 5 * 60) {
            hud.classList.add('warning');
        } else {
            hud.classList.remove('warning');
        }
    }
    setSessionHudVisible(true);
}

function applySessionOverlay(data) {
    if (!data || data.active !== true) {
        sessionActive = false;
        sessionEndsAtMs = null;
        sessionOpenEnded = false;
        sessionPackageName = '';
        setSessionHudVisible(false);
        return;
    }

    sessionActive = true;
    sessionPackageName = data.package_name || 'Sesi aktif';
    sessionOpenEnded = String(data.billing_mode || '').toLowerCase() === 'openended';

    if (sessionOpenEnded || !data.ends_at) {
        sessionEndsAtMs = null;
    } else {
        var parsed = Date.parse(data.ends_at);
        sessionEndsAtMs = isNaN(parsed) ? null : parsed;
    }

    updateSessionHudTick();
}

function syncHomeWithState(hdmiHasSignal) {
    // Sesi aktif → sembunyikan HOME (tampilkan HDMI + HUD), termasuk di emulator.
    if (sessionActive) {
        setHomeVisible(false);
        return;
    }
    if (typeof hdmiHasSignal === 'boolean') {
        setHomeVisible(!hdmiHasSignal);
        return;
    }
    setHomeVisible(true);
}

function initTVWindow() {
    if (typeof tizen === 'undefined' || !tizen.systeminfo || !tizen.tvwindow) {
        console.log('TVWindow API not available (emulator?) — keep home unless session active');
        syncHomeWithState(false);
        return;
    }

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
            syncHomeWithState(false);
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
                        syncHomeWithState(false);
                    },
                    ['0px', '0px', '100%', '100%'],
                    'MAIN'
                );
            },
            function (error) {
                console.log('TVWindow setSource error: ' + error.message);
                syncHomeWithState(false);
            }
        );
    }, function (error) {
        console.log('VIDEOSOURCE error: ' + error.message);
        syncHomeWithState(false);
    });
}

function checkSignal() {
    try {
        if (sessionActive) {
            syncHomeWithState(true);
            return;
        }

        if (typeof tizen === 'undefined' || !tizen.tvwindow) {
            syncHomeWithState(false);
            return;
        }

        var source = tizen.tvwindow.getSource();
        if (!source || source.signal === null || typeof source.signal === 'undefined') {
            syncHomeWithState(false);
            return;
        }

        syncHomeWithState(source.signal === true);
    } catch (error) {
        console.log('checkSignal error: ' + error.message);
        syncHomeWithState(false);
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
        var warnUrl = BILLING_SERVER_URL + '/api/tv-notification?tv_id=' + encodeURIComponent(TV_ID);
        var warnResponse = await fetch(warnUrl);
        if (warnResponse.ok) {
            var warnData = await warnResponse.json();
            if (warnData && warnData.show_warning === true) {
                showWarningBanner(warnData.message);
            }
        }
    } catch (error) {
        // Network errors must not disrupt checkSignal / TVWindow loop
    }

    try {
        var sessionUrl = BILLING_SERVER_URL + '/api/tv-session?tv_id=' + encodeURIComponent(TV_ID);
        var sessionResponse = await fetch(sessionUrl);
        if (!sessionResponse.ok) {
            return;
        }
        var sessionData = await sessionResponse.json();
        applySessionOverlay(sessionData);
        checkSignal();
    } catch (error) {
        // ignore
    }
}

var init = function () {
    console.log('init() called');

    updateHomeClock();
    clockTimer = setInterval(function () {
        updateHomeClock();
        updateSessionHudTick();
    }, 1000);

    setHomeVisible(true);
    initTVWindow();
    setInterval(checkSignal, 2000);
    setInterval(checkBillingStatus, 3000);
    checkBillingStatus();

    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) {
            initTVWindow();
            updateHomeClock();
            checkBillingStatus();
        }
    });

    // Remote dipegang operator — jangan exit app (dipakai sebagai HOME / last open app)
    document.addEventListener('keydown', function (e) {
        switch (e.keyCode) {
        case 10009: // RETURN — tetap di app
            e.preventDefault();
            break;
        default:
            break;
        }
    });
};

window.onload = init;
