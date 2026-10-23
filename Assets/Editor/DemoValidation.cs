using System;
using SiegeDemo;
using UnityEngine;

public static class DemoValidation
{
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("DEMO_VALIDATION: " + message); }
    public static void Run(LevelData data)
    {
        var s = new LaunchSettings();
        foreach (float p in new[] {.151f,.399f,.401f,.699f,.701f,1.2f})
        {
            var aim = LauncherController.Evaluate(new Vector2(1,.2f).normalized * p * s.maxDrag, s);
            int expected = p < .4f ? 1 : p < .7f ? 2 : 3;
            Require(aim.valid && aim.tier == expected, "tier at " + p);
            Require(Mathf.Abs(aim.velocity.magnitude - s.maxSpeed * expected / 3) < .01f, "fixed tier speed");
        }
        Require(!LauncherController.Evaluate(Vector2.zero,s).valid, "cancel zone");
        Require(!LauncherController.Evaluate(Vector2.left,s).valid, "forward drag cancellation");
        Require(Mathf.Abs(LauncherController.Evaluate(Vector2.right * s.maxDrag,s).angle - s.minAngle)<.01f, "minimum angle");
        Require(Mathf.Abs(LauncherController.Evaluate(new Vector2(.01f,10),s).angle - s.maxAngle)<.01f, "maximum angle");
        var invalid = LevelConfiguration.Parse(LevelConfiguration.Serialize(data));
        invalid.ammunition = 0;
        Require(LevelConfiguration.Validate(invalid).Count > 0,"invalid ammo rejected");
        invalid = LevelConfiguration.Parse(LevelConfiguration.Serialize(data));
        invalid.objects[0].partId = "missing-part";
        Require(LevelConfiguration.Validate(invalid).Count > 0,"missing part rejected");
        invalid = LevelConfiguration.Parse(LevelConfiguration.Serialize(data));
        if (invalid.objects.Length > 1) { invalid.objects[1].id = invalid.objects[0].id; Require(LevelConfiguration.Validate(invalid).Count>0,"duplicate id rejected"); }
        Debug.Log("DEMO_CONTRACT_CHECKS_SUCCESS: tiers, cancellation, angle limits, config roundtrip and invalid data");
    }
}
