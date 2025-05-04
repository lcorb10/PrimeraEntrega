using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 2f;

    private Vector2 direction;
    private Rigidbody2D rb;
    private GameObject shooter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetDirection(Vector2 dir, GameObject shooterObj)
    {
        shooter = shooterObj;

        // Aplica una ligera variación aleatoria al ángulo
        float angleOffset = Random.Range(-5f, 5f);
        direction = Quaternion.Euler(0, 0, angleOffset) * dir.normalized;

        // Ignorar colisión con quien disparó
        Collider2D bulletCollider = GetComponent<Collider2D>();
        Collider2D shooterCollider = shooter.GetComponent<Collider2D>();
        if (bulletCollider != null && shooterCollider != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, shooterCollider);
        }

        // Aplica la velocidad
        rb.linearVelocity = direction * speed;

        // Destruye la bala tras un tiempo
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Se destruye si colisiona con algo que no sea el que disparó
        if (collision.gameObject != shooter)
        {
            Destroy(gameObject);
        }
    }
}
