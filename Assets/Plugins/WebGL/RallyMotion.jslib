// Tilt input for the web build. Unity's own accelerometer path on Android Chrome gives up unless
// navigator.permissions reports "granted", which is often not the case inside an iframe (itch.io).
// Three browser sources are tried and the first one that delivers data wins:
//   1. `devicemotion` (accelerationIncludingGravity)
//   2. Generic Sensor API `Accelerometer` (started directly, without the permissions.query gate)
//   3. `deviceorientation` (beta / gamma angles turned into a gravity vector)
// Sensors are listened to from the start; where the browser also exposes requestPermission (iOS, and
// recent Chrome on Android) it is additionally requested on each tap until data arrives.
// Values are returned in Unity's convention (g units, upright portrait = (0, -1, 0), tilting right
// gives +x) and already rotated to the screen orientation, so X is always "screen right".
mergeInto(LibraryManager.library, {
  RallyMotion_Start: function () {
    if (window.__rallyMotion) return;
    var m = window.__rallyMotion = { x: 0, y: 0, z: 0, samples: 0, source: 0, flags: 0, error: '', perm: '' };
    // flags: 1 motion API, 2 motion data, 4 sensor started, 8 sensor data, 16 orientation data,
    //        32 permission denied, 64 sensor error, 128 orientation API, 256 insecure page
    var g = 9.80665;
    if (window.isSecureContext === false) m.flags |= 256;
    // Browsers report the reaction to gravity (Android: upright = +y); iOS reports it inverted.
    var mult = /(iPhone|iPad|Macintosh)/i.test(navigator.userAgent) ? 1 / g : -1 / g;

    function screenAngle() {
      if (screen.orientation && typeof screen.orientation.angle === 'number') return screen.orientation.angle;
      return typeof window.orientation === 'number' ? window.orientation : 0;
    }

    // rank: lower is preferred; a source only overwrites values from a source of equal or worse rank.
    function store(rank, ax, ay, az) {
      if (m.source !== 0 && rank > m.source) return;
      m.source = rank;
      var x = ax * mult, y = ay * mult, z = az * mult;
      var r = screenAngle() * Math.PI / 180, c = Math.cos(r), s = Math.sin(r);
      m.x = x * c - y * s;
      m.y = x * s + y * c;
      m.z = z;
      m.samples++;
    }

    function onMotion(e) {
      var a = e.accelerationIncludingGravity;
      if (!a || a.x === null || a.y === null) return;
      m.flags |= 2;
      store(1, a.x, a.y, a.z || 0);
    }

    function onOrientation(e) {
      if (e.beta === null || e.gamma === null) return;
      m.flags |= 16;
      var b = e.beta * Math.PI / 180, gm = e.gamma * Math.PI / 180;
      // Gravity reaction in device axes (same convention as devicemotion on Android).
      store(3, -g * Math.cos(b) * Math.sin(gm), g * Math.sin(b), g * Math.cos(b) * Math.cos(gm));
    }

    function startSensor() {
      if (typeof Accelerometer === 'undefined') return;
      try {
        var sensor = new Accelerometer({ frequency: 60, referenceFrame: 'device' });
        sensor.addEventListener('reading', function () { m.flags |= 8; store(2, sensor.x, sensor.y, sensor.z); });
        sensor.addEventListener('error', function (e) { m.flags |= 64; m.error = String(e.error && e.error.name || e.error || e); });
        sensor.start();
        m.flags |= 4;
      } catch (err) { m.flags |= 64; m.error = String(err && err.name || err); }
    }

    function listen() {
      if (typeof DeviceMotionEvent !== 'undefined') { m.flags |= 1; window.addEventListener('devicemotion', onMotion); }
      if (typeof DeviceOrientationEvent !== 'undefined') { m.flags |= 128; window.addEventListener('deviceorientation', onOrientation); }
      startSensor();
    }

    // Listen straight away: Android browsers deliver data without asking (even the recent Chrome versions
    // that also expose requestPermission), and on iOS listening early is harmless until permission arrives.
    listen();

    var needsPermission = typeof DeviceMotionEvent !== 'undefined' && typeof DeviceMotionEvent.requestPermission === 'function';
    if (needsPermission) {
      // Permission can only be requested from a user gesture. Ask on every tap until there is data:
      // a refused first attempt (e.g. the tap that started the stage) must not disable tilt for good.
      var asking = false;
      var ask = function () {
        if (m.samples > 0) {
          window.removeEventListener('touchend', ask, true);
          window.removeEventListener('click', ask, true);
          return;
        }
        if (asking) return;
        asking = true;
        DeviceMotionEvent.requestPermission().then(function (state) {
          asking = false;
          m.perm = state;
          if (state === 'granted') m.flags &= ~32; else m.flags |= 32;
        }).catch(function (err) {
          asking = false;
          m.flags |= 32;
          m.perm = String(err && (err.name + ': ' + err.message) || err);
        });
        if (typeof DeviceOrientationEvent !== 'undefined' && typeof DeviceOrientationEvent.requestPermission === 'function')
          DeviceOrientationEvent.requestPermission().catch(function () {});
      };
      window.addEventListener('touchend', ask, true);
      window.addEventListener('click', ask, true);
    }
  },

  // Short text for the pause-menu diagnosis: permission answer or the browser's sensor error.
  RallyMotion_Detail: function () {
    var m = window.__rallyMotion;
    var text = m ? (m.perm || m.error || '') : '';
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  RallyMotion_X: function () { return window.__rallyMotion ? window.__rallyMotion.x : 0; },
  RallyMotion_Y: function () { return window.__rallyMotion ? window.__rallyMotion.y : 0; },
  RallyMotion_Z: function () { return window.__rallyMotion ? window.__rallyMotion.z : 0; },
  RallyMotion_Samples: function () { return window.__rallyMotion ? window.__rallyMotion.samples : 0; },
  RallyMotion_Source: function () { return window.__rallyMotion ? window.__rallyMotion.source : 0; },
  RallyMotion_Flags: function () { return window.__rallyMotion ? window.__rallyMotion.flags : 0; }
});
