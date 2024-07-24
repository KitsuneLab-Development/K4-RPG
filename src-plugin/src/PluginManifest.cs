namespace K4RPG
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "K4-RPG Core";

        public override string ModuleDescription => "A modular RPG system for Counter-Strike2";

        public override string ModuleAuthor => "K4ryuu @ KitsuneLab";

        public override string ModuleVersion => "1.0.3 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}