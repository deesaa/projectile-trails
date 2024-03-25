using UnityEngine;

// This is just an example, you can use these callbacks if you want.
// Projectiles will be added and removed cyclically,
// so you may expect projectile indices to be persistent.


namespace TrailRenderer
{
    public class ExampleProjectileTrailRenderer : MonoBehaviour
    {
        public Gun gun;
        public UnityEngine.TrailRenderer tracePrefab;
        UnityEngine.TrailRenderer[] _traces;

        private void Start()
        {
            if (gun != null)
            {
                gun.onProjectileCreated += OnProjectileCreated;
                gun.onProjectileRemoved += OnProjectileRemoved;
                gun.onProjectileMoved += OnProjectileMoved;
            }

            _traces = new UnityEngine.TrailRenderer[gun.maxProjectileCount];
            for (int index = 0; index < _traces.Length; ++index)
            {
                UnityEngine.TrailRenderer trace = Instantiate(tracePrefab);
                trace.gameObject.SetActive(false);
                _traces[index] = trace;
            }
        }

        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The created projectile.</param>
        private void OnProjectileCreated(int index, ref Gun.Projectile projectile)
        {
            _traces[index].transform.position = projectile.position;
            _traces[index].Clear();
            _traces[index].gameObject.SetActive(true);
        }

        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The removed projectile.</param>
        private void OnProjectileRemoved(int index, ref Gun.Projectile projectile)
        {
            _traces[index].gameObject.SetActive(false);
        }

        /// <summary>
        /// A callback that is called every frame while a projectile is moving.
        /// </summary>
        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The moved projectile.</param>
        private void OnProjectileMoved(int index, ref Gun.Projectile projectile)
        {
            _traces[index].transform.position = projectile.position;
        }
    }
}