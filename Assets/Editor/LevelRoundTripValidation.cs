using System;
using System.IO;
using SiegeDemo;
using SiegeDemo.Editor;
using UnityEngine;

public static class LevelRoundTripValidation
{
    public static void Run(LevelData original)
    {
        string directory = Path.GetFullPath(".setup/validation");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "roundtrip.json");
        string initial = LevelConfiguration.Serialize(original);
        File.WriteAllText(path, initial);
        GameObject preview = null;
        try
        {
            preview = LevelAuthoringTools.LoadFile(path);
            var markers = preview.GetComponentsInChildren<LevelObjectMarker>();
            if (markers.Length != LevelConfiguration.Expand(original).Count) throw new Exception("Preview object count mismatch");
            LevelObjectMarker target = Array.Find(markers, marker => Array.Find(original.parts,p=>p.id==marker.partId).kind==PartKind.Target);
            string targetId = target.objectId;
            Vector3 changedPosition = target.transform.position + Vector3.right * .1f;
            target.transform.position = changedPosition;
            LevelAuthoringTools.SavePreview(path);
            var saved = LevelConfiguration.Parse(File.ReadAllText(path));
            if (saved.structures.Length != 0 || saved.templates.Length != original.templates.Length || saved.ammunition != original.ammunition) throw new Exception("Save changed non-placement data");
            var savedTarget = Array.Find(saved.objects,p=>p.id==targetId);
            if ((savedTarget.position-(Vector2)changedPosition).sqrMagnitude>.00001f) throw new Exception("Manual movement was not saved");
            preview = LevelAuthoringTools.LoadFile(path);
            var duplicate = new GameObject("duplicate marker");
            duplicate.transform.SetParent(preview.transform);
            var copy = duplicate.AddComponent<LevelObjectMarker>();
            copy.objectId = targetId; copy.partId = savedTarget.partId;
            duplicate.transform.position = changedPosition;
            duplicate.AddComponent<BoxCollider2D>().size = Array.Find(saved.parts,p=>p.id==savedTarget.partId).size;
            bool rejected=false;
            try { LevelAuthoringTools.SavePreview(path); } catch(FormatException) { rejected=true; }
            if(!rejected) throw new Exception("Duplicate scene ID was saved");
            if(File.ReadAllText(path)!=LevelConfiguration.Serialize(saved)) throw new Exception("Failed save touched original file");
            UnityEngine.Object.DestroyImmediate(duplicate);
            rejected=false;
            try { LevelAuthoringTools.LoadPreview("{\"version\":99}",path); } catch(FormatException) { rejected=true; }
            if(!rejected || !preview) throw new Exception("Invalid load removed existing preview");
            if(LevelConfiguration.Serialize(original)!=initial) throw new Exception("Runtime/editor capture mutated original configuration");
            Debug.Log("LEVEL_AUTHORING_ROUNDTRIP_SUCCESS: actual preview, manual edit, save/load, duplicate rejection and original preservation");
        }
        finally { if(preview) UnityEngine.Object.DestroyImmediate(preview); }
    }
}
