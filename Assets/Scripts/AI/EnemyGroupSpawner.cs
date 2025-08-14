using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    public class EnemyGroupSpawner : MonoBehaviour
    {
        public EnemyGroup groupPrefab;
        public GameObject enemyPrefab;
        public SpawnerAnchor spawnerPrefab;
        public Material spawnerMaterial;

        public KeyCode spawnGroupKey = KeyCode.G;
        public KeyCode duplicateGroupKey = KeyCode.H;
        public KeyCode spawnEnemyKey = KeyCode.J;

        void Update()
        {
            if (Input.GetKeyDown(spawnGroupKey)) SpawnGroupNearCamera();
            if (Input.GetKeyDown(duplicateGroupKey)) DuplicateActiveGroup();
            if (Input.GetKeyDown(spawnEnemyKey)) SpawnEnemyInActiveGroup();
        }

        void SpawnGroupNearCamera()
        {
            var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (!cam) { Debug.LogError("GroupSpawner: No camera."); return; }

            Vector3 basePos = cam.transform.position + cam.transform.forward * 6f;
            basePos.y = 0f;

            var spawner = Instantiate(spawnerPrefab, basePos, Quaternion.identity);
            spawner.visualMaterial = spawnerMaterial;

            var group = Instantiate(groupPrefab, basePos, Quaternion.identity);
            group.enemyPrefab = enemyPrefab;
            group.homeAnchor = spawner.transform;
            group.spawnPoint = spawner.transform;
            group.ResetHome();

            EnemyGroup.Active = group;
            Debug.Log("Spawned EnemyGroup + SpawnerAnchor (linked).");
        }

        void DuplicateActiveGroup()
        {
            if (EnemyGroup.Active == null) { Debug.LogWarning("No Active group."); return; }

            Vector3 offset = Vector3.right * (EnemyGroup.Active.patrolRadius * 2f + 2f);
            var spawnerClone = Instantiate(spawnerPrefab, EnemyGroup.Active.homeAnchor.position + offset, Quaternion.identity);
            spawnerClone.visualMaterial = spawnerMaterial;

            var clone = Instantiate(EnemyGroup.Active.gameObject, spawnerClone.transform.position, Quaternion.identity).GetComponent<EnemyGroup>();
            clone.enemyPrefab = EnemyGroup.Active.enemyPrefab;
            clone.homeAnchor = spawnerClone.transform;
            clone.spawnPoint = spawnerClone.transform;
            clone.ResetHome();

            EnemyGroup.Active = clone;
            Debug.Log("Duplicated EnemyGroup with its own SpawnerAnchor.");
        }

        void SpawnEnemyInActiveGroup()
        {
            if (EnemyGroup.Active == null) { Debug.LogWarning("No Active group."); return; }
            if (EnemyGroup.Active.enemyPrefab == null) { Debug.LogError("Active group missing enemyPrefab."); return; }
            EnemyGroup.Active.SpawnEnemy();
        }
    }
}



