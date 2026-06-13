#pragma once

#include <cstdint>
#include <string>
#include "Event.h"
#include "../DTO/PartyMemberData.h"

namespace L2Bot::Domain::Events
{
	enum class PartyMemberAction : uint8_t
	{
		Added = 0,
		Updated = 1,
		Removed = 2,
		All = 3,  // PartySmallWindowAll — full refresh
	};

	class PartyMemberUpdatedEvent : public Event
	{
	public:
		static constexpr const char* name = "partyMemberUpdated";

		const std::string GetName() const override
		{
			return std::string(name);
		}

		const DTO::PartyMemberData& GetMember() const
		{
			return m_Member;
		}

		PartyMemberAction GetAction() const
		{
			return m_Action;
		}

		PartyMemberUpdatedEvent(const DTO::PartyMemberData member, PartyMemberAction action) :
			m_Member(member),
			m_Action(action)
		{
		}

		PartyMemberUpdatedEvent() = delete;
		virtual ~PartyMemberUpdatedEvent() = default;

	private:
		const DTO::PartyMemberData m_Member;
		const PartyMemberAction m_Action;
	};
}
