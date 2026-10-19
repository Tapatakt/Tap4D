using System.Numerics;
using System.Windows.Input;
using Tap4D.Maths;
using Tap4D.Scene;

namespace Tap4D.Viewer;

/// <summary>
/// First-person camera controller. The Y axis is special: pitch (YZ), roll (XY) and
/// lean (YW) never affect movement. Orientation composes as
/// XY(roll) · YW(lean) · YZ(pitch) · [XZ(yaw) · XW(wheel) · ZW]: roll/lean are innermost
/// (pitch never changes their objective planes), pitch applies before the movement frame
/// (mouse up always looks up), and yaw is the innermost of the movement frame, so mouse
/// left/right always turns the view toward screen left/right no matter the XW/ZW state
/// (with yaw outermost, an XW flip of 180° made mouse look inverted).
/// Roll and lean are quick ±45° tilts that spring back when released.
/// </summary>
internal class FpsCameraController
{
    private const float MoveSpeed = 1.5f;
    private const float RotateSpeed = 1.0f;
    private const float MouseSensitivity = 0.003f;
    private const float WheelStep = 0.2f;
    private const float PitchLimit = 80f * MathF.PI / 180f;
    private const float LeanLimit = 45f * MathF.PI / 180f;
    private const float LeanSpeed = 8f;

    private readonly HashSet<Key> _pressed = [];
    private float _yaw;
    private float _pitch;
    private float _wheelXw;
    private float _zw;
    private float _roll;
    private float _lean;
    private float _rollTarget;
    private float _leanTarget;

    /// <summary>
    /// Handles a key press: movement keys are tracked, Q/E set the roll target
    /// (and cancel lean), Z/X rotate in the ZW plane while held.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Press(Key key)
    {
        if (key == Key.Q)
            (_rollTarget, _leanTarget) = (LeanLimit, 0);
        else if (key == Key.E)
            (_rollTarget, _leanTarget) = (-LeanLimit, 0);
        _pressed.Add(key);
    }

    /// <summary>
    /// Handles a key release; roll/lean targets are recomputed from the keys still held.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Release(Key key)
    {
        _pressed.Remove(key);
        RecomputeRollTarget();
    }

    /// <summary>
    /// Handles a mouse lean button (XButton1/XButton2): sets the lean target
    /// (and cancels roll), or clears it on release.
    /// </summary>
    /// <param name="sign">The lean direction.</param>
    /// <param name="pressed">True on press, false on release.</param>
    public void OnLeanButton(int sign, bool pressed)
    {
        if (pressed)
            (_leanTarget, _rollTarget) = (sign * LeanLimit, 0);
        else
            _leanTarget = 0;
    }

    /// <summary>
    /// Handles raw mouse movement: horizontal motion yaws (XZ), vertical pitches (YZ, ±80°).
    /// </summary>
    /// <param name="dx">The horizontal mouse delta in raw input counts.</param>
    /// <param name="dy">The vertical mouse delta in raw input counts.</param>
    public void OnMouseDelta(int dx, int dy)
    {
        _yaw -= dx * MouseSensitivity;
        _pitch = Math.Clamp(_pitch + dy * MouseSensitivity, -PitchLimit, PitchLimit);
    }

    /// <summary>
    /// Handles mouse wheel rotation (XW plane).
    /// </summary>
    /// <param name="delta">The wheel delta (120 per notch).</param>
    public void OnWheel(int delta) => _wheelXw += delta / 120f * WheelStep;

    /// <summary>
    /// Applies the current input state to the camera for one frame: springs roll/lean,
    /// moves (Y objectively, X/Z/W in the movement frame) and composes the orientation.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="dt">The elapsed time since the previous frame, in seconds.</param>
    public void Update(Camera4 camera, float dt)
    {
        _roll = Approach(_roll, _rollTarget, LeanSpeed * dt);
        _lean = Approach(_lean, _leanTarget, LeanSpeed * dt);
        if (_pressed.Contains(Key.Z))
            _zw += RotateSpeed * dt;
        if (_pressed.Contains(Key.X))
            _zw -= RotateSpeed * dt;
        Matrix4x4 moveFrame = MoveFrame();
        float speed = MoveSpeed * dt;
        // move forward/backward
        if (_pressed.Contains(Key.W))
            camera.Position += Vector4.Transform(Vector4.UnitZ * speed, moveFrame);
        if (_pressed.Contains(Key.S))
            camera.Position -= Vector4.Transform(Vector4.UnitZ * speed, moveFrame);
        // move left/right
        if (_pressed.Contains(Key.A))
            camera.Position -= Vector4.Transform(Vector4.UnitX * speed, moveFrame);
        if (_pressed.Contains(Key.D))
            camera.Position += Vector4.Transform(Vector4.UnitX * speed, moveFrame);
        // move ana/kata
        if (_pressed.Contains(Key.T))
            camera.Position += Vector4.Transform(Vector4.UnitW * speed, moveFrame);
        if (_pressed.Contains(Key.G))
            camera.Position -= Vector4.Transform(Vector4.UnitW * speed, moveFrame);
        // move objecttive up/down
        if (_pressed.Contains(Key.R))
            camera.Position += Vector4.UnitY * speed;
        if (_pressed.Contains(Key.F))
            camera.Position -= Vector4.UnitY * speed;
        camera.Orientation = Rotation4.Create(RotationPlane.XY, _roll) * Rotation4.Create(RotationPlane.YW, _lean)
            * Rotation4.Create(RotationPlane.YZ, _pitch) * moveFrame;
    }

    /// <summary>
    /// Composes the movement frame: the part of the orientation without Y-involving
    /// rotations. Yaw applies first (in the camera-local XZ plane), then the wheel XW
    /// and the ZW rotations follow the heading.
    /// </summary>
    /// <returns>The movement frame matrix.</returns>
    private Matrix4x4 MoveFrame() =>
        Rotation4.Create(RotationPlane.XZ, _yaw)
        * Rotation4.Create(RotationPlane.XW, _wheelXw)
        * Rotation4.Create(RotationPlane.ZW, _zw);

    /// <summary>
    /// Recomputes the roll target from the roll keys still held.
    /// </summary>
    private void RecomputeRollTarget() =>
        _rollTarget = _pressed.Contains(Key.Q) ? LeanLimit : _pressed.Contains(Key.E) ? -LeanLimit : 0;

    /// <summary>
    /// Moves a value toward a target by at most the given step.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <param name="target">The target value.</param>
    /// <param name="step">The maximum change.</param>
    /// <returns>The new value.</returns>
    private static float Approach(float value, float target, float step) =>
        value < target ? MathF.Min(value + step, target) : MathF.Max(value - step, target);
}
