#pragma once

#include <map>
#include <shared_mutex>
#include "../GameStructs/NetworkHandlerWrapper.h"
#include "Domain/Repositories/EntityRepositoryInterface.h"
#include "../Factories/NPCFactory.h"
#include "Domain/Events/CreatureDiedEvent.h"
#include "../../GameStructs/FindObjectsTrait.h"
#include "Domain/Services/ServiceLocator.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	class NPCRepository : public Repositories::EntityRepositoryInterface, public FindObjectsTrait
	{
	public:
		const std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> GetEntities() override
		{
			// Phase 1: walk game memory WITHOUT lock — this can take 100ms+ and
			// blocking the game thread (which fires OnSpoiled / OnCreatureDied)
			// during that window would freeze the game.
			const auto allCreatures = FindAllObjects<User*>(m_Radius, [this](float_t radius, int32_t prevId) {
				return m_NetworkHandler.GetNextCreature(radius, prevId);
			});

			// Phase 2: lock only for m_Npcs manipulation — fast path.
			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);

			std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> result;
			for (const auto kvp : allCreatures) {
				const auto creature = kvp.second;
				if (creature->userType != L2::UserType::NPC) {
					continue;
				}

				if (m_Npcs.find(creature->objectId) == m_Npcs.end()) {
					m_Npcs[creature->objectId] = m_Factory.Create(creature);
				}
				else
				{
					m_Factory.Update(m_Npcs[creature->objectId], creature);
				}

				result[creature->objectId] = m_Npcs[creature->objectId];
			}

			return result;
		}

		void Reset() override
		{
			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);
			m_Npcs.clear();
		}

		void Init() override
		{
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::CreatureDiedEvent::name, [this](const Events::Event& evt) {
				OnCreatureDied(evt);
			});
		}

		NPCRepository(const NetworkHandlerWrapper& networkHandler, const NPCFactory& factory, const uint16_t radius) :
			m_NetworkHandler(networkHandler),
			m_Factory(factory),
			m_Radius(radius)
		{
		}

		NPCRepository() = delete;
		virtual ~NPCRepository() = default;

		void OnCreatureDied(const Events::Event& evt)
		{
			std::unique_lock<std::shared_timed_mutex> lock(m_Mutex);
			if (evt.GetName() == Events::CreatureDiedEvent::name)
			{
				const auto casted = static_cast<const Events::CreatureDiedEvent&>(evt);
				if (m_Npcs.find(casted.GetCreatureId()) != m_Npcs.end()) {
					m_Npcs[casted.GetCreatureId()]->MarkAsDead();
				}
			}
		}

	private:
		const NPCFactory& m_Factory;
		const NetworkHandlerWrapper& m_NetworkHandler;
		const uint16_t m_Radius = 0;
		std::shared_timed_mutex m_Mutex;
		std::unordered_map<uint32_t, std::shared_ptr<Entities::NPC>> m_Npcs;
	};
}