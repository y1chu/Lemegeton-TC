using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;
using System.Linq;

namespace Lemegeton.Core
{

    internal class Alliance
    {

        public List<Party> Alliances = new List<Party>();

    }

    internal class Party
    {

        public class PartyMember
        {

            public int Index { get; set; }
            public string Name { get; set; }
            public IGameObject GameObject { get; set; } = null;
            public ulong ObjectId { get; set; } = 0;
            public uint EntityId => GameObject?.EntityId ?? (uint)ObjectId;
            public int Selection { get; set; } = 0;

            public uint Job => GameObject is IBattleChara character ? character.ClassJob.RowId : 0;

            public float X
            {
                get
                {
                    return GameObject != null ? GameObject.Position.X : 0.0f;
                }
            }

            public float Y
            {
                get
                {
                    return GameObject != null ? GameObject.Position.Y : 0.0f;
                }
            }

            public float Z
            {
                get
                {
                    return GameObject != null ? GameObject.Position.Z : 0.0f;
                }
            }

        }

        public List<PartyMember> Members { get; set; } = new List<PartyMember>();

        // Missing or duplicate HUD slots must not become marker priorities.
        public bool HasValidOrder => Members.Count > 0 && Members.Count <= 8
            && Members.All(member => member.Index >= 1 && member.Index <= Members.Count)
            && Members.Select(member => member.Index).Distinct().Count() == Members.Count;

        public PartyMember GetByIndex(int index)
        {
            if (!HasValidOrder || index < 1 || index > 8)
            {
                return null;
            }
            return Members.Find(member => member.Index == index);
        }

        public PartyMember GetByActorId(uint actorId)
        {
            if (actorId == 0 || actorId == 0xE0000000)
            {
                return null;
            }
            return Members.Find(member => member.EntityId == actorId);
        }

        public List<PartyMember> GetByActorIds(IEnumerable<uint> actorIds)
        {
            return (from ix in Members
                    where ix.EntityId != 0 && ix.EntityId != 0xE0000000
                    join jx in actorIds on ix.EntityId equals jx select ix).ToList();
        }

    }

}
