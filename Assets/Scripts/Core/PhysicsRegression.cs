using System;
using System.Collections;
using UnityEngine;

namespace SiegeDemo
{
    // Runs only in the development player's explicit -smokeTest path.
    public static class PhysicsRegression
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var root = new GameObject("Smoke physics fixtures");
            var a = Body(root.transform, "paired beam", new Vector2(80,0), Vector2.one, false);
            var b = Body(root.transform, "paired target", new Vector2(82,0), Vector2.one, true);
            a.GetComponent<Rigidbody2D>().gravityScale = 0;
            b.GetComponent<Rigidbody2D>().gravityScale = 0;
            int aHits=0,bHits=0;
            a.Damaged += (body, damage) => aHits++;
            b.Damaged += (body, damage) => bHits++;
            a.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(10,0);
            yield return new WaitForSeconds(.5f);
            check(aHits == 1 && bHits == 1, "both callback sides damage once per actual collision");
            check(a.HitPoints < 100 && b.HitPoints < 100, "normal impulse damages both participating bodies");
            int destroys=0;
            a.Destroyed += body => destroys++;
            a.Eliminate(); a.Eliminate();
            check(destroys == 1, "destruction is idempotent");
            yield return null;
            check(!a, "destroyed entity removes physical support");
            UnityEngine.Object.Destroy(root);
            yield return null;

            root = new GameObject("Smoke collapse fixture");
            var floor = new GameObject("fixture floor");
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector2(80,-4);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(8,1);
            var target = Body(root.transform, "supported target", new Vector2(80,-3),Vector2.one,true);
            int targetHits=0;
            target.Damaged += (body,damage) => targetHits++;
            yield return new WaitForSeconds(.6f);
            check(targetHits == 0 && target.HitPoints == 100, "resting support excludes gravity impulse");
            var falling = Body(root.transform, "falling beam", new Vector2(80,0),new Vector2(2,.3f),false);
            falling.GetComponent<Rigidbody2D>().mass = 3;
            yield return new WaitForSeconds(1.3f);
            check(targetHits > 0 && target.HitPoints < 100, "falling structure physically damages target");
            UnityEngine.Object.Destroy(root);
            yield return null;
            Debug.Log("DEMO_PHYSICS_REGRESSION_COMPLETE");
        }
        private static DamageableBody Body(Transform root,string name,Vector2 position,Vector2 size,bool target)
        {
            var go=new GameObject(name);
            go.transform.SetParent(root);
            go.transform.position=position;
            go.AddComponent<BoxCollider2D>().size=size;
            var rb=go.AddComponent<Rigidbody2D>();
            rb.mass=1;
            rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var body=go.AddComponent<DamageableBody>();
            body.Initialize(new PartDefinition { id=name,kind=target?PartKind.Target:PartKind.Beam,size=size,hitPoints=100,impulseThreshold=.5f,damageMultiplier=1,mass=1 });
            return body;
        }
    }
}
