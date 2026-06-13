using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Domain.DTO;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Events;
using Newtonsoft.Json;

namespace Client.Domain.Service
{
    public class PartyHandler : HandlerInterface
    {
        private readonly EventBusInterface eventBus;
        private readonly Dictionary<uint, PartyMember> partyMembers = new();

        public IReadOnlyDictionary<uint, PartyMember> PartyMembers => partyMembers;

        public void Update(MessageOperationEnum operation, string content)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<PartyMemberDto>(content);
                if (data == null) return;

                switch (operation)
                {
                    case MessageOperationEnum.Create:
                    case MessageOperationEnum.Update:
                        var member = new PartyMember
                        {
                            ObjectId = data.ObjectId,
                            Name = data.Name ?? "",
                            Level = data.Level,
                            ClassId = data.ClassId,
                            Hp = data.Hp,
                            HpMax = data.HpMax,
                            Mp = data.Mp,
                            MpMax = data.MpMax,
                            Cp = data.Cp,
                            CpMax = data.CpMax,
                        };
                        partyMembers[member.ObjectId] = member;
                        eventBus.Publish(new PartyMemberCreatedEvent(member));
                        break;

                    case MessageOperationEnum.Delete:
                        partyMembers.Remove(data.ObjectId);
                        eventBus.Publish(new PartyMemberDeletedEvent(data.ObjectId));
                        break;
                }
            }
            catch { }
        }

        public PartyHandler(EventBusInterface eventBus)
        {
            this.eventBus = eventBus;
        }

        private class PartyMemberDto
        {
            public uint ObjectId { get; set; }
            public string? Name { get; set; }
            public int Level { get; set; }
            public int ClassId { get; set; }
            public int Hp { get; set; }
            public int HpMax { get; set; }
            public int Mp { get; set; }
            public int MpMax { get; set; }
            public int Cp { get; set; }
            public int CpMax { get; set; }
        }
    }
}
