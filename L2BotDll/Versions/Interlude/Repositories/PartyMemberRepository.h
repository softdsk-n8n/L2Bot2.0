#pragma once

#include <vector>
#include <shared_mutex>
#include <unordered_map>
#include "Domain/Events/PartyMemberUpdatedEvent.h"
#include "Domain/Services/ServiceLocator.h"
#include "Domain/Entities/EntityInterface.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	/// Stores current party member state. Updated from network packets.
	class PartyMemberRepository : public Repositories::EntityRepositoryInterface
	{
	public:
		const std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> GetEntities() override
		{
			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);
			return m_Members;
		}

		void Reset() override
		{
			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);
			m_Members.clear();
		}

		void Init() override
		{
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(
				Events::PartyMemberUpdatedEvent::name,
				[this](const Events::Event& evt) { OnPartyMemberUpdated(evt); }
			);
		}

		PartyMemberRepository() = default;
		virtual ~PartyMemberRepository() = default;

	private:
		void OnPartyMemberUpdated(const Events::Event& evt)
		{
			if (evt.GetName() != Events::PartyMemberUpdatedEvent::name) return;

			const auto& casted = static_cast<const Events::PartyMemberUpdatedEvent&>(evt);
			const auto& member = casted.GetMember();
			auto action = casted.GetAction();

			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);

			if (action == Events::PartyMemberAction::Removed)
			{
				m_Members.erase(member.objectId);
			}
			else
			{
				// Store raw data as a simple entity for serialization
				auto entity = std::make_shared<PartyMemberEntity>(member);
				m_Members[member.objectId] = entity;
			}
		}

		/// Lightweight entity wrapper for PartyMemberData so it fits EntityRepositoryInterface.
		struct PartyMemberEntity : public Entities::EntityInterface
		{
			const uint32_t GetId() const override { return data.objectId; }
			const std::string GetEntityName() const override { return "partyMember"; }

			const std::size_t GetHash() const override
			{
				// Combine all fields into a hash for change detection
				std::size_t h = std::hash<uint32_t>{}(data.objectId);
				h ^= std::hash<int32_t>{}(data.hp) + (h << 6) + (h >> 2);
				h ^= std::hash<int32_t>{}(data.hpMax) + (h << 6) + (h >> 2);
				h ^= std::hash<int32_t>{}(data.mp) + (h << 6) + (h >> 2);
				h ^= std::hash<int32_t>{}(data.mpMax) + (h << 6) + (h >> 2);
				h ^= std::hash<int32_t>{}(data.cp) + (h << 6) + (h >> 2);
				return h;
			}

			const std::vector<Serializers::Node> BuildSerializationNodes() const override
			{
				return {
					{ L"objectId", std::to_wstring(data.objectId) },
					{ L"name", data.name },
					{ L"level", std::to_wstring(data.level) },
					{ L"classId", std::to_wstring(data.classId) },
					{ L"hp", std::to_wstring(data.hp) },
					{ L"hpMax", std::to_wstring(data.hpMax) },
					{ L"mp", std::to_wstring(data.mp) },
					{ L"mpMax", std::to_wstring(data.mpMax) },
					{ L"cp", std::to_wstring(data.cp) },
					{ L"cpMax", std::to_wstring(data.cpMax) },
				};
			}

			DTO::PartyMemberData data;

			PartyMemberEntity(const DTO::PartyMemberData& d) : data(d) {}
		};

		std::unordered_map<uint32_t, std::shared_ptr<Entities::EntityInterface>> m_Members;
		std::shared_timed_mutex m_Mutex;
	};
}
