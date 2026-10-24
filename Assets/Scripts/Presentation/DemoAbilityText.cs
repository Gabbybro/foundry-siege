namespace SiegeDemo
{
    internal static class DemoAbilityText
    {
        internal static string Name(ActiveAbility value)
        {
            switch (value)
            {
                case ActiveAbility.Split: return "空中分裂";
                case ActiveAbility.Dash: return "空中冲刺";
                default: return "未选择主动";
            }
        }
        internal static string Description(ActiveAbility value)
        {
            switch (value)
            {
                case ActiveAbility.Split: return "飞行中单击，将母体替换为三枚较轻子弹，沿当前飞行方向扇形展开。子弹继承构筑并刷新各自被动机会。";
                case ActiveAbility.Dash: return "飞行中点击有效方向，瞬间改向；速度取当前实际速度加冲刺增量，随后继续受重力与碰撞影响。";
                default: return "正式开局选择一种主动；练习可直接配置。";
            }
        }
        internal static string Name(PassiveAbility value)
        {
            switch (value)
            {
                case PassiveAbility.WoodPiercing: return "穿木一次";
                case PassiveAbility.GroundBounce: return "触地反弹";
                case PassiveAbility.RestExplosion: return "停驻爆破";
                case PassiveAbility.DeathShock: return "连锁冲击";
                case PassiveAbility.GlassShards: return "破片扩散";
                case PassiveAbility.StoneRicochet: return "石面跳弹";
                case PassiveAbility.ApexDive: return "顶点俯冲";
                default: return "未知被动";
            }
        }
        internal static string Description(PassiveAbility value)
        {
            switch (value)
            {
                case PassiveAbility.WoodPiercing: return "达到速度与厚度条件后穿过一块木构件，保留部分速度；条件不足不消耗机会。";
                case PassiveAbility.GroundBounce: return "从上方触及明确标记的地面后向上反弹，每枚原弹或分裂子弹各一次。";
                case PassiveAbility.RestExplosion: return "有支撑且持续低速时倒计时爆破，直接伤害并推动；重新移动会中断。";
                case PassiveAbility.DeathShock: return "击杀装甲目标后产生受遮挡的范围伤害与推力，可通过后续击杀继续连锁。";
                case PassiveAbility.GlassShards: return "原弹或分裂子弹直接撞碎玻璃时发射两枚有伤害破片，每发共享一次。";
                case PassiveAbility.StoneRicochet: return "成功擦撞石面后向上弹起并保留部分前进速度；正撞不消耗机会。";
                case PassiveAbility.ApexDive: return "每枚弹体在自然顶点后向下俯冲一次；分裂子弹重新获得机会。";
                default: return "配置中尚未识别的能力。";
            }
        }
    }
}
