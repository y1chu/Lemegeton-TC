namespace Lemegeton.Core
{

    internal abstract class Automarker : ContentItem
    {

        public override FeaturesEnum Features => _state.cfg.UseSoftmarkers || AsSoftmarker
            ? FeaturesEnum.Drawing : FeaturesEnum.Automarker;

        [DebugOption]
        [AttributeOrderNumber(500)]
        public bool SelfMarkOnly { get; set; }

        [DebugOption]
        [AttributeOrderNumber(501)]
        public bool AsSoftmarker { get; set; }

        public Automarker(State state) : base(state)
        {
        }

    }

}
