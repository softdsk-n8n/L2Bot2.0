using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Domain.Entities;

namespace Client.Domain.Events
{
    public class PartyMemberCreatedEvent : EventInterface
    {
        public PartyMember PartyMember { get; }
        public PartyMemberCreatedEvent(PartyMember partyMember)
        {
            PartyMember = partyMember;
        }
    }

    public class PartyMemberDeletedEvent : EventInterface
    {
        public uint ObjectId { get; }
        public PartyMemberDeletedEvent(uint objectId)
        {
            ObjectId = objectId;
        }
    }
}
