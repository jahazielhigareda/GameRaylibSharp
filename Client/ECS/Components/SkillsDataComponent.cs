namespace Client.ECS.Components;

/// <summary>Arch struct component – skills received from server for HUD display.</summary>
public struct SkillsDataComponent
{
    public int FistLevel;       public int FistPercent;
    public int ClubLevel;       public int ClubPercent;
    public int SwordLevel;      public int SwordPercent;
    public int AxeLevel;        public int AxePercent;
    public int DistanceLevel;   public int DistancePercent;
    public int ShieldingLevel;  public int ShieldingPercent;
    public int FishingLevel;    public int FishingPercent;
    public int MagicLevel;      public int MagicPercent;
}
