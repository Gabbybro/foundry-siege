using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    // Explicit development utility: runs actual level physics to find a reproducible solution.
    public sealed class DemoAimSearch : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var battle=GetComponent<BattleController>();
            Time.timeScale=30;
            var chosen=new List<float>();
            for(int shot=0;shot<3;shot++)
            {
                float bestScore=float.NegativeInfinity,bestAngle=5;
                foreach(float angle in new[]{5f,7.5f,10f,12.5f,15f,17.5f,20f,25f,30f,40f,50f,60f,70f,75f})
                {
                    battle.Restart();
                    while(battle.State==BattleState.Preparing) yield return null;
                    foreach(float previous in chosen)
                    {
                        battle.TryLaunch(Shot(previous,battle));
                        while(battle.State==BattleState.Resolving) yield return null;
                    }
                    if(battle.State==BattleState.Ready) battle.TryLaunch(Shot(angle,battle));
                    while(battle.State==BattleState.Resolving) yield return null;
                    float hp=0;
                    string remaining="";
                    foreach(var body in battle.World.GetComponentsInChildren<DamageableBody>())
                        if(body.IsTarget) { hp+=body.HitPoints; remaining+=body.name+":"+body.HitPoints.ToString("F2")+","; }
                    float score=-battle.TargetsRemaining*100-hp;
                    Debug.Log("AIM_SEARCH: prefix="+string.Join(",",chosen)+" angle="+angle+" score="+score+" remaining="+remaining);
                    if(battle.State==BattleState.Won)
                    {
                        chosen.Add(angle);
                        Debug.Log("AIM_SEARCH_SOLUTION: "+string.Join(",",chosen));
                        Application.Quit(0); yield break;
                    }
                    if(score>bestScore) {bestScore=score;bestAngle=angle;}
                }
                chosen.Add(bestAngle);
                Debug.Log("AIM_SEARCH_CHOSEN: "+string.Join(",",chosen));
            }
            Debug.LogError("AIM_SEARCH_NO_SOLUTION");
            Application.Quit(3);
        }
        private static LaunchSolution Shot(float angle,BattleController battle)
        {
            float speed=battle.Data.launch.maxSpeed;
            return new LaunchSolution{valid=true,tier=3,angle=angle,speed=speed,velocity=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*speed};
        }
    }
}
