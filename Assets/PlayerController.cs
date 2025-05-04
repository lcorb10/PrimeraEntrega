using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Movimiento")]
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private float fuerzaSalto = 10f;
    [SerializeField] private float tiempoSaltoMax = 0.3f;

    [Header("Suelo")]
    [SerializeField] private float longitudRaycast = 0.2f;
    [SerializeField] private LayerMask capaSuelo;

    [Header("Dash")]
    [SerializeField] private float distanciaDash = 3f;
    [SerializeField] private float duracionDash = 0.5f;
    [SerializeField] private float cooldownDash = 7f;

    [Header("Disparo")]
    [SerializeField] private float fireRate = 0.4f;
    private float fireCooldown;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;

    private Vector2 colliderOriginalSize;
    private Vector2 colliderOriginalOffset;

    private bool enSuelo;
    private bool agachado;
    private bool atacandoAgachado;
    private bool estaSaltando;
    private bool estaDasheando = false;
    private bool puedeDashear = true;
    private bool mantenerPosicion = false;

    private float inputX;
    private float inputY;
    private bool saltoPulsado;
    private bool saltoPresionado;
    private bool ataquePresionado;
    private float tiempoSaltoActual;
    private bool apuntandoAnterior = false;
    private bool disparando;

    private readonly int paramMovement = Animator.StringToHash("movement");
    private readonly int paramEnSuelo = Animator.StringToHash("enSuelo");
    private readonly int paramAgachado = Animator.StringToHash("agachado");
    private readonly int paramAtackAgachado = Animator.StringToHash("atackAgachado");

    private string animacionDisparoActual = "";

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        colliderOriginalSize = boxCollider.size;
        colliderOriginalOffset = boxCollider.offset;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Update()
    {
        LeerEntrada();

        if (mantenerPosicion && saltoPulsado)
        {
            saltoPulsado = false;
            saltoPresionado = false;
            ResetearAnimacionesDisparo();
            return;
        }

        if (!estaDasheando && puedeDashear && InputDashDetectado())
        {
            StartCoroutine(RealizarDash());
        }

        if (estaDasheando)
        {
            animator.Play("dash");
        }

        animator.SetBool("isDashing", estaDasheando);
        fireCooldown -= Time.deltaTime;

        if (ataquePresionado && fireCooldown <= 0f)
        {
            Disparar();
            fireCooldown = fireRate;
        }

        // Nueva lógica para animación atackStraight automática
        if (disparando && mantenerPosicion && Mathf.Abs(inputX) < 0.1f)
        {
            if (animacionDisparoActual != "atackStraight")
            {
                ResetearAnimacionesDisparo();
                animacionDisparoActual = "atackStraight";
                animator.SetBool(animacionDisparoActual, true);
            }
        }
        else if (animacionDisparoActual == "atackStraight" && !disparando)
        {
            animator.SetBool("atackStraight", false);
            animacionDisparoActual = "";
        }

        if (!ataquePresionado && !string.IsNullOrEmpty(animacionDisparoActual))
        {
            ResetearAnimacionesDisparo();
        }

        if (mantenerPosicion && !ataquePresionado && !estaDasheando)
        {
            animator.Play("idle");
        }
        bool apuntando = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.JoystickButton5);

        if (disparando && Mathf.Abs(inputX) < 0.01f && !apuntando && string.IsNullOrEmpty(animacionDisparoActual))
        {
            animator.SetBool("atackStraight", true);
        }
        else
        {
            if (!disparando)
            {
                animator.SetBool("atackStraight", false);
            }

        }


    }

    private void FixedUpdate()
    {
        DetectarSuelo();
        ManejarAgachado();
        ManejarAtaque();

        if (!estaDasheando)
        {
            if (!atacandoAgachado)
            {
                Movimiento();
                Salto();
            }
            else
            {
                DetenerMovimientoHorizontal();
            }
        }
        else
        {
            DetenerMovimientoHorizontal();
        }

        ActualizarAnimaciones();
        saltoPulsado = false;
    }

    private void LeerEntrada()
    {
        var gamepad = Gamepad.current;

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            inputX = stick.x;
            inputY = stick.y;

            mantenerPosicion = gamepad.rightShoulder.isPressed;

            if (mantenerPosicion && !apuntandoAnterior)
            {
                rb.linearVelocity = Vector2.zero;
                ResetearAnimacionesDisparo();
                animator.Play("idle");
            }

            if (gamepad.buttonSouth.wasPressedThisFrame) saltoPulsado = true;
            saltoPresionado = gamepad.buttonSouth.isPressed;
            ataquePresionado = gamepad.buttonWest.isPressed;
        }
        else
        {
            inputX = (Input.GetKey(KeyCode.A) ? -1f : 0f) + (Input.GetKey(KeyCode.D) ? 1f : 0f);
            inputY = Input.GetKey(KeyCode.W) ? 1f : (Input.GetKey(KeyCode.S) ? -1f : 0f);

            mantenerPosicion = Input.GetKey(KeyCode.C);

            if (mantenerPosicion && !apuntandoAnterior)
            {
                rb.linearVelocity = Vector2.zero;
                ResetearAnimacionesDisparo();
                animator.Play("idle");
            }

            if (Input.GetKeyDown(KeyCode.Space)) saltoPulsado = true;
            saltoPresionado = Input.GetKey(KeyCode.Space);
            ataquePresionado = Input.GetKey(KeyCode.J) || Input.GetMouseButton(0);
        }

        apuntandoAnterior = mantenerPosicion;
        disparando = ataquePresionado; // Se actualiza aquí
    }



private bool InputDashDetectado()
{
    var keyboard = Keyboard.current;
    var gamepad = Gamepad.current;

    return (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)) ||
           (gamepad != null && gamepad.buttonEast.wasPressedThisFrame); // botón B (Xbox) o Círculo (PS)
}


    private IEnumerator RealizarDash()
    {
        agachado = false;
        atacandoAgachado = false;
        ActualizarCollider();

        estaDasheando = true;
        puedeDashear = false;

        Vector2 direccion = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 inicio = rb.position;
        Vector2 destino = inicio + direccion * distanciaDash;

        float tiempo = 0f;
        while (tiempo < duracionDash)
        {
            rb.MovePosition(Vector2.Lerp(inicio, destino, tiempo / duracionDash));
            tiempo += Time.deltaTime;
            yield return null;
        }

        rb.MovePosition(destino);
        estaDasheando = false;

        yield return new WaitForSeconds(cooldownDash);
        puedeDashear = true;
    }

    private void DetectarSuelo()
    {
        Vector2 origen = boxCollider.bounds.center;
        RaycastHit2D hit = Physics2D.Raycast(origen, Vector2.down, longitudRaycast, capaSuelo);
        enSuelo = hit.collider != null;
    }

    private void Movimiento()
    {
        if (agachado || atacandoAgachado) return;

        if (mantenerPosicion)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            if (inputX > 0)
                transform.localScale = new Vector3(1, 1, 1);
            else if (inputX < 0)
                transform.localScale = new Vector3(-1, 1, 1);

            return;
        }

        float velocidadFinal = inputX * velocidad;
        rb.linearVelocity = new Vector2(velocidadFinal, rb.linearVelocity.y);

        animator.SetFloat(paramMovement, Mathf.Abs(velocidadFinal));

        if (inputX > 0) transform.localScale = new Vector3(1, 1, 1);
        else if (inputX < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    private void Salto()
    {
        if (enSuelo && saltoPulsado && !agachado)
        {
            estaSaltando = true;
            tiempoSaltoActual = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, fuerzaSalto);
            ResetearAnimacionesDisparo();
        }

        if (estaSaltando && saltoPresionado)
        {
            tiempoSaltoActual += Time.fixedDeltaTime;
            if (tiempoSaltoActual < tiempoSaltoMax)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, fuerzaSalto);
            }
        }

        if (!saltoPresionado || rb.linearVelocity.y <= 0)
        {
            estaSaltando = false;
        }
    }

    private void ManejarAgachado()
    {
        bool quiereAgacharse = inputY < -0.5f && enSuelo;

        if (mantenerPosicion && !agachado)
        {
            return;
        }

        if (quiereAgacharse && !agachado)
        {
            agachado = true;
            ActualizarCollider();
        }
        else if (!quiereAgacharse && agachado)
        {
            agachado = false;
            atacandoAgachado = false;
            ActualizarCollider();
        }
    }

    private void ManejarAtaque()
    {
        if (agachado && enSuelo && ataquePresionado)
        {
            if (!atacandoAgachado)
            {
                atacandoAgachado = true;
                ActualizarCollider();
            }
        }
        else if (atacandoAgachado)
        {
            atacandoAgachado = false;
            ActualizarCollider();
        }
    }

    private void ActualizarCollider()
    {
        bool enEstadoReducido = agachado || atacandoAgachado;
        float ajusteAltura = colliderOriginalSize.y / 4f;

        if (enEstadoReducido)
        {
            boxCollider.size = new Vector2(colliderOriginalSize.x, colliderOriginalSize.y / 2f);
            boxCollider.offset = new Vector2(colliderOriginalOffset.x, colliderOriginalOffset.y - ajusteAltura);
            spriteRenderer.transform.localPosition = new Vector3(spriteRenderer.transform.localPosition.x, colliderOriginalOffset.y - ajusteAltura, spriteRenderer.transform.localPosition.z);
        }
        else
        {
            boxCollider.size = colliderOriginalSize;
            boxCollider.offset = colliderOriginalOffset;
            spriteRenderer.transform.localPosition = new Vector3(spriteRenderer.transform.localPosition.x, colliderOriginalOffset.y, spriteRenderer.transform.localPosition.z);
        }
    }

    private void Disparar()
    {
        Vector2 direccion = new Vector2(inputX, inputY);
        if (direccion == Vector2.zero)
        {
            direccion = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        }
        direccion.Normalize();
        firePoint.right = direccion;

        ActivarAnimacionDisparo(direccion);

        Vector3 offset;
        if (agachado || atacandoAgachado)
            offset = new Vector3(0f, 0.25f, 0f);
        else
            offset = new Vector3(0, Random.Range(-0.1f, 0.1f), 0f);

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position + offset, Quaternion.identity);
        bullet.GetComponent<Bullet>().SetDirection(direccion, gameObject);
    }

    private void ActivarAnimacionDisparo(Vector2 direccion)
    {
        if (!string.IsNullOrEmpty(animacionDisparoActual))
            animator.SetBool(animacionDisparoActual, false);

        animacionDisparoActual = "";

        if (agachado || atacandoAgachado)
        {
            animacionDisparoActual = "atackAgachado";
        }
        else if (mantenerPosicion)
        {
            bool estaQuieto = Mathf.Abs(inputX) < 0.1f;

            if (direccion.y > 0.5f)
                animacionDisparoActual = Mathf.Abs(direccion.x) > 0.1f ? "atackDiagonalUp" : "atackUp";
            else if (direccion.y < -0.5f)
                animacionDisparoActual = Mathf.Abs(direccion.x) > 0.1f ? "atackDiagonalDown" : "atackDown";
            else if (estaQuieto)
                animacionDisparoActual = "atackStraight";
        }

        if (!string.IsNullOrEmpty(animacionDisparoActual))
            animator.SetBool(animacionDisparoActual, true);
    }

    private void ResetearAnimacionesDisparo()
    {
        if (!string.IsNullOrEmpty(animacionDisparoActual))
        {
            animator.SetBool(animacionDisparoActual, false);
            animacionDisparoActual = "";
        }
    }

    private void DetenerMovimientoHorizontal()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private void ActualizarAnimaciones()
    {
        animator.SetBool(paramEnSuelo, enSuelo);
        animator.SetBool(paramAgachado, agachado || atacandoAgachado);
        animator.SetBool(paramAtackAgachado, atacandoAgachado);
    }

    private void OnDrawGizmosSelected()
    {
        if (boxCollider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(boxCollider.bounds.center, boxCollider.bounds.center + Vector3.down * longitudRaycast);
        }
    }
}
