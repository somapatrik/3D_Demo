using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{
    // --- Nastavitelné v Inspectoru (po buildu) ---
    [Export] public float WalkSpeed { get; set; } = 5.0f;        // m/s
    [Export] public float SprintSpeed { get; set; } = 9.0f;      // m/s
    [Export] public float JumpVelocity { get; set; } = 5.5f;     // m/s nahoru
    [Export] public float Acceleration { get; set; } = 12.0f;    // jak rychle se rozběhne/zastaví
    [Export] public float TurnSpeed { get; set; } = 12.0f;       // jak rychle se model natočí
    [Export] public float MouseSensitivity { get; set; } = 0.003f; // radiány na pixel
    [Export] public float MinPitchDegrees { get; set; } = -70f;  // jak moc se dá koukat dolů
    [Export] public float MaxPitchDegrees { get; set; } = 35f;   // jak moc nahoru

    private Node3D _cameraPivot = null!;
    private SpringArm3D _springArm = null!;
    private Node3D _model = null!;

    public override void _Ready()
    {
        // Cesty jsou relativní k tomuto nodu a odpovídají jménům ve stromu scény.
        _cameraPivot = GetNode<Node3D>("CameraPivot");
        _springArm = GetNode<SpringArm3D>("CameraPivot/SpringArm3D");
        _model = GetNode<Node3D>("Model");

        // SpringArm nesmí narážet do vlastní postavy, jinak kamera "skáče" k hlavě.
        _springArm.AddExcludedObject(GetRid());

        // Schová kurzor a zamkne ho do okna.
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool captured = Input.MouseMode == Input.MouseModeEnum.Captured;

        if (@event is InputEventMouseMotion motion && captured)
        {
            // Do stran: otáčíme pivotem kolem osy Y.
            _cameraPivot.RotateY(-motion.Relative.X * MouseSensitivity);

            // Nahoru/dolů: náklon spring armu kolem osy X, s omezením.
            Vector3 rot = _springArm.Rotation;
            rot.X = Mathf.Clamp(
                rot.X - motion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees));
            _springArm.Rotation = rot; // Rotation je struct, musí se zapsat zpátky celý
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible; // Esc uvolní myš
        }
        else if (@event is InputEventMouseButton { Pressed: true } && !captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured; // klik ji znovu zamkne
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector3 velocity = Velocity;

        // 1) Gravitace (hodnota z Project Settings › Physics › 3D).
        if (!IsOnFloor())
            velocity += GetGravity() * dt;

        // 2) Skok, jen když stojíme na zemi.
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // 3) Vstup z klávesnice jako 2D vektor (x = vlevo/vpravo, y = vpřed/vzad).
        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");

        // 4) Převod do světa relativně ke kameře: "W" = tam, kam se dívá kamera.
        Basis cam = _cameraPivot.GlobalTransform.Basis;
        Vector3 forward = new Vector3(-cam.Z.X, 0, -cam.Z.Z).Normalized();
        Vector3 right = new Vector3(cam.X.X, 0, cam.X.Z).Normalized();
        Vector3 direction = right * input.X - forward * input.Y;

        // 5) Plynulé zrychlení k cílové rychlosti (nezávislé na FPS).
        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        Vector3 target = direction * speed;
        float blend = 1f - Mathf.Exp(-Acceleration * dt);
        velocity.X = Mathf.Lerp(velocity.X, target.X, blend);
        velocity.Z = Mathf.Lerp(velocity.Z, target.Z, blend);

        // 6) Pohyb s kolizemi: klouže po zdech, leze po rampách.
        Velocity = velocity;
        MoveAndSlide();

        // 7) Natočení modelu směrem pohybu. Model kouká na −Z.
        if (direction.LengthSquared() > 0.001f)
        {
            float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
            Vector3 modelRot = _model.Rotation;
            modelRot.Y = Mathf.LerpAngle(modelRot.Y, targetYaw, 1f - Mathf.Exp(-TurnSpeed * dt));
            _model.Rotation = modelRot;
        }
    }
}
