using AdequateEnough;
using UnityEngine;

public class KeycardHUD : MonoBehaviour
{
    [SerializeField] private GameObject sectorAIndicator;
    [SerializeField] private GameObject sectorBIndicator;
    [SerializeField] private GameObject sectorCIndicator;

    private void OnEnable()  => PlayerController.OnKeycardUpgraded += OnKeycardUpgraded;
    private void OnDisable() => PlayerController.OnKeycardUpgraded -= OnKeycardUpgraded;

    private void Start()
    {
        SetAll(KeycardLevel.None);
    }

    private void OnKeycardUpgraded(KeycardLevel level) => SetAll(level);

    private void SetAll(KeycardLevel level)
    {
        if (sectorAIndicator != null) sectorAIndicator.SetActive(level >= KeycardLevel.SectorA);
        if (sectorBIndicator != null) sectorBIndicator.SetActive(level >= KeycardLevel.SectorB);
        if (sectorCIndicator != null) sectorCIndicator.SetActive(level >= KeycardLevel.SectorC);
    }
}
