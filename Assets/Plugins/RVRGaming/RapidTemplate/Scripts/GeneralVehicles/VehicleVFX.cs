using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
using System.Collections.Generic;

public class VehicleVFX : MonoBehaviour
{
    [Header("Damage VFX")]
    public GameObject damageVFXPrefab;
    public Vector3 damageVFXOffset;
    public Vector3 damageVFXRotation;
    private GameObject damageVFXInstance;
    private VisualEffect damageVFX;
    private Coroutine damageStopCoroutine;

    [Header("Exhaust VFX")]
    public GameObject exhaustVFXPrefab;
    public Vector3 exhaustVFXOffset;
    public Vector3 exhaustVFXRotation;
    private GameObject exhaustVFXInstance;
    private VisualEffect exhaustVFX;

    [Header("Skid Marks (LineRenderer Based)")]
    public Material skidMarkMaterial;
    public float skidFadeTime = 2f;
    private Dictionary<WheelCollider, LineRenderer> currentSkidLines = new Dictionary<WheelCollider, LineRenderer>();
    private Dictionary<WheelCollider, Coroutine> fadeCoroutines = new Dictionary<WheelCollider, Coroutine>();

    [Header("Drift Smoke VFX")]
    public GameObject driftSmokePrefab;
    public int driftSmokePoolSize = 10;
    public float driftSmokeLifetime = 5f;

    private List<GameObject> driftSmokePool = new List<GameObject>();
    private Dictionary<GameObject, Coroutine> smokeDeactivationCoroutines = new Dictionary<GameObject, Coroutine>();

    private PhysicsCarController carController;
    private PhysicsBikeController bikeController;
    private HoverVehicleController hoverController;
    private WheelCollider[] wheelColliders;
    private bool previousDrifting = false;

    void Start()
    {
        carController = GetComponent<PhysicsCarController>();
        bikeController = GetComponent<PhysicsBikeController>();
        hoverController = GetComponent<HoverVehicleController>();

        if (carController != null)
        {
            wheelColliders = new[] {
                carController.frontLeftWheelCollider,
                carController.frontRightWheelCollider,
                carController.rearLeftWheelCollider,
                carController.rearRightWheelCollider
            };
        }
        else if (bikeController != null)
        {
            wheelColliders = new[] {
                bikeController.frontWheelCollider,
                bikeController.rearWheelCollider
            };
        }
        else if (hoverController != null)
        {
            wheelColliders = new WheelCollider[0];
        }
        else
        {
            Debug.LogError("No valid vehicle controller found on the GameObject.");
            return;
        }

        if (damageVFXPrefab != null)
        {
            damageVFXInstance = Instantiate(damageVFXPrefab, transform);
            damageVFXInstance.transform.localPosition = damageVFXOffset;
            damageVFXInstance.transform.localEulerAngles = damageVFXRotation;
            damageVFX = damageVFXInstance.GetComponent<VisualEffect>();
            damageVFXInstance.SetActive(false);
        }

        if (exhaustVFXPrefab != null)
        {
            exhaustVFXInstance = Instantiate(exhaustVFXPrefab, transform);
            exhaustVFXInstance.transform.localPosition = exhaustVFXOffset;
            exhaustVFXInstance.transform.localEulerAngles = exhaustVFXRotation;
            exhaustVFX = exhaustVFXInstance.GetComponent<VisualEffect>();
            exhaustVFXInstance.SetActive(false);
        }

        foreach (WheelCollider wheel in wheelColliders)
        {
            currentSkidLines[wheel] = null;
            fadeCoroutines[wheel] = null;
        }

        if (driftSmokePrefab != null)
        {
            for (int i = 0; i < driftSmokePoolSize; i++)
            {
                GameObject poolObj = Instantiate(driftSmokePrefab, transform);
                poolObj.SetActive(false);
                driftSmokePool.Add(poolObj);
            }
        }
    }

    void Update()
    {
        HandleDamageVFX();
        HandleExhaustVFX();
    }

    void FixedUpdate()
    {
        HandleSkidMarks();
        HandleDriftSmoke();
    }

    private void HandleDamageVFX()
    {
        if (damageVFXInstance == null || damageVFX == null)
            return;

        float health = 0f;
        if (carController != null)
            health = (float)carController.currentHealth.Get(gameObject);
        else if (bikeController != null)
            health = (float)bikeController.currentHealth.Get(gameObject);
        else if (hoverController != null)
            health = (float)hoverController.currentHealth.Get(gameObject);

        if (health <= 0)
        {
            if (damageStopCoroutine != null)
            {
                StopCoroutine(damageStopCoroutine);
                damageStopCoroutine = null;
            }
            if (!damageVFXInstance.activeSelf)
                damageVFXInstance.SetActive(true);
            if (!damageVFX.isActiveAndEnabled)
                damageVFX.Play();
        }
        else
        {
            if (damageVFXInstance.activeSelf && damageStopCoroutine == null)
            {
                damageVFX.Stop();
                damageStopCoroutine = StartCoroutine(StopDamageVFXCoroutine());
            }
        }
    }

    private IEnumerator StopDamageVFXCoroutine()
    {
        yield return new WaitForSeconds(5f);
        damageVFXInstance.SetActive(false);
        damageStopCoroutine = null;
    }

    private void HandleExhaustVFX()
    {
        if (exhaustVFXInstance == null || exhaustVFX == null)
            return;

        bool isEnabled = false;
        if (carController != null)
            isEnabled = carController.isVehicleEnabled;
        else if (bikeController != null)
            isEnabled = bikeController.isVehicleEnabled;
        else if (hoverController != null)
            isEnabled = hoverController.isVehicleEnabled;

        float health = carController != null
            ? (float)carController.currentHealth.Get(gameObject)
            : bikeController != null
                ? (float)bikeController.currentHealth.Get(gameObject)
                : hoverController != null
                    ? (float)hoverController.currentHealth.Get(gameObject)
                    : 0f;

        float fuel = carController != null
            ? (float)carController.currentFuel.Get(gameObject)
            : bikeController != null
                ? (float)bikeController.currentFuel.Get(gameObject)
                : hoverController != null
                    ? (float)hoverController.currentFuel.Get(gameObject)
                    : 0f;

        if (isEnabled && health > 0 && fuel > 0)
        {
            if (!exhaustVFXInstance.activeSelf)
            {
                exhaustVFXInstance.SetActive(true);
                exhaustVFX.Play();
            }
        }
        else
        {
            if (exhaustVFXInstance.activeSelf)
            {
                exhaustVFX.Stop();
                exhaustVFXInstance.SetActive(false);
            }
        }
    }

    private void HandleSkidMarks()
    {
        if (hoverController != null) return;

        bool isDrifting = false;
        if (carController != null)
            isDrifting = carController.isDrifting || carController.handbrakeInput;
        else if (bikeController != null)
            isDrifting = bikeController.isDrifting || bikeController.handbrakeInput;

        if (isDrifting && !previousDrifting)
        {
            foreach (WheelCollider wheel in wheelColliders)
            {
                var skidObj = new GameObject($"SkidLine_{wheel.name}_{Time.time}");
                skidObj.transform.parent = transform;
                var line = skidObj.AddComponent<LineRenderer>();
                line.material = new Material(skidMarkMaterial);
                line.textureMode = LineTextureMode.Tile;
                line.material.mainTextureScale = new Vector2(1, 1);
                line.startWidth = 0.2f;
                line.endWidth = 0.2f;
                line.positionCount = 0;
                currentSkidLines[wheel] = line;
            }
        }

        if (isDrifting)
        {
            foreach (var wheel in wheelColliders)
            {
                if (wheel.GetGroundHit(out WheelHit hit))
                {
                    var line = currentSkidLines[wheel];
                    if (line == null) continue;

                    var skidPoint = hit.point + Vector3.up * 0.01f;
                    if (line.positionCount < 2)
                    {
                        line.positionCount = 2;
                        line.SetPosition(0, skidPoint);
                        line.SetPosition(1, skidPoint);
                    }
                    else
                    {
                        line.positionCount++;
                        line.SetPosition(line.positionCount - 1, skidPoint);
                    }
                }
            }
        }
        else if (previousDrifting)
        {
            foreach (var wheel in wheelColliders)
            {
                var line = currentSkidLines[wheel];
                if (line != null && line.positionCount > 1 && fadeCoroutines[wheel] == null)
                    fadeCoroutines[wheel] = StartCoroutine(FadeSkidMarks(line, wheel));

                currentSkidLines[wheel] = null;
            }
        }

        previousDrifting = isDrifting;
    }

    private void HandleDriftSmoke()
    {
        if (hoverController != null) return;

        bool isDrifting = false;
        if (carController != null)
            isDrifting = carController.isDrifting || carController.handbrakeInput;
        else if (bikeController != null)
            isDrifting = bikeController.isDrifting || bikeController.handbrakeInput;

        if (isDrifting)
        {
            foreach (var wheel in wheelColliders)
            {
                if (!wheel.GetGroundHit(out WheelHit hit)) continue;

                GameObject smokeObj = GetDriftSmokeFromPool();
                if (smokeObj == null)
                {
                    foreach (var poolObj in driftSmokePool)
                    {
                        if (poolObj.activeInHierarchy && smokeDeactivationCoroutines.ContainsKey(poolObj))
                        {
                            StopCoroutine(smokeDeactivationCoroutines[poolObj]);
                            smokeDeactivationCoroutines.Remove(poolObj);
                            smokeObj = poolObj;
                            break;
                        }
                    }
                }

                if (smokeObj == null) continue;

                smokeObj.transform.position = hit.point + Vector3.up * 0.1f;
                if (!smokeObj.activeSelf)
                {
                    smokeObj.SetActive(true);
                    StartSmoke(smokeObj);
                }
                else if (smokeDeactivationCoroutines.ContainsKey(smokeObj))
                {
                    StopCoroutine(smokeDeactivationCoroutines[smokeObj]);
                    smokeDeactivationCoroutines.Remove(smokeObj);
                    StartSmoke(smokeObj);
                }
            }
        }
        else
        {
            foreach (var smokeObj in driftSmokePool)
            {
                if (smokeObj.activeInHierarchy && !smokeDeactivationCoroutines.ContainsKey(smokeObj))
                {
                    var cor = StartCoroutine(StopSmokeEffect(smokeObj));
                    smokeDeactivationCoroutines[smokeObj] = cor;
                }
            }
        }
    }

    private GameObject GetDriftSmokeFromPool()
    {
        foreach (GameObject smoke in driftSmokePool)
        {
            if (!smoke.activeInHierarchy)
                return smoke;
        }
        return null;
    }

    private void StartSmoke(GameObject smokeObj)
    {
        VisualEffect vfx = smokeObj.GetComponent<VisualEffect>();
        if (vfx != null)
            vfx.Play();
        else
        {
            ParticleSystem ps = smokeObj.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();
        }
    }

    private IEnumerator StopSmokeEffect(GameObject smokeObj)
    {
        VisualEffect vfx = smokeObj.GetComponent<VisualEffect>();
        if (vfx != null)
            vfx.Stop();
        else
        {
            ParticleSystem ps = smokeObj.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Stop();
        }
        yield return new WaitForSeconds(driftSmokeLifetime);
        smokeObj.SetActive(false);
        smokeDeactivationCoroutines.Remove(smokeObj);
    }

    private IEnumerator FadeSkidMarks(LineRenderer line, WheelCollider wheel)
    {
        Color startColor = line.material.color;
        float elapsedTime = 0f;
        while (elapsedTime < skidFadeTime)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / skidFadeTime);
            line.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        Destroy(line.gameObject);
        fadeCoroutines[wheel] = null;
    }
}