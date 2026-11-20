using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;

namespace Lemegeton.Core
{

    internal class AutomarkerPayload
    {

        private readonly State _st;
        public bool markSelfOnly { get; set; } = false;
        public bool softMarker { get; set; } = false;

        public Dictionary<AutomarkerSigns.SignEnum, List<IGameObject>> assignments = new Dictionary<AutomarkerSigns.SignEnum, List<IGameObject>>();

        public AutomarkerPayload(State st, bool selfOnly, bool soft)
        {
            _st = st;
            markSelfOnly = selfOnly;
            softMarker = _st.cfg.UseSoftmarkers || soft;
        }

        public void Assign(AutomarkerSigns.SignEnum sign, Party.PartyMember pm)
        {
            Assign(sign, pm?.GameObject);
        }
        
        public void Assign(AutomarkerSigns.SignEnum sign, IGameObject go)
        {
            if (go == null)
            {
                return;
            }
            if (markSelfOnly == true)
            {
                var localPlayer = _st.cs?.LocalPlayer;
                if (localPlayer == null || go.GameObjectId != localPlayer.GameObjectId)
                {
                    return;
                }
            }
            if (sign != AutomarkerSigns.SignEnum.AttackNext && sign != AutomarkerSigns.SignEnum.BindNext && sign != AutomarkerSigns.SignEnum.IgnoreNext)
            {
                assignments[sign] = new List<IGameObject>(new IGameObject[] { go });
            }
            else
            {
                if (!assignments.TryGetValue(sign, out var actors))
                {
                    actors = new List<IGameObject>();
                    assignments[sign] = actors;
                }
                actors.Add(go);
            }
        }

    }

}
