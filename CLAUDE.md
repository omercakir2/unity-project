# CLAUDE.md

Unity 6 (6000.6.5f1, URP 17) üçüncü şahıs korku/hayatta kalma oyunu. Tek geliştirici: Omer Cakir. Kullanıcıyla Türkçe konuş.

## Her zaman geçerli kurallar

1. **Kullanıcı açıkça istemedikçe hiçbir değişiklik commit veya push edilmez.** Değişiklikleri çalışma ağacında bırak, ne yaptığını raporla. PR açma; commit istenirse `main`'e yapılır.
2. **Netleşmeyen bir kısmı kendi kafana göre rastgele yapma.** Anlamadığın veya birden fazla makul yorumu olan her şeyi (değerler, animasyonlar, davranış, hangi sahne/prefab) uygulamadan önce sor.
3. Kullanıcının çalışma ağacındaki commit edilmemiş değişikliklere (silinmiş sahneler, değiştirilmiş `.unity` dosyaları) dokunma, geri alma.

## Yapı

- `Assets/Game/` — tüm oyun içeriği. `Assets/Imported/` üçüncü taraf paketler (düzenleme).
- `Assets/Game/Scripts/` — namespace yok, alt klasörler: `Player/`, `Camera/`, `Inventory/` (silahlar, lightball, hotbar), `UI/`, `World/`, `Zombie/`, `Facility/`, `Editor/`.
- `Assets/Game/Scripts/Editor/Setup*.cs` — sahne/prefab/animator kurulumu `Tools/...` menü komutlarıyla yapılır (örn. `Tools/Setup Inventory Hotbar`, `Tools/Setup Throw Animation`). Kalıcı prefab/animator değişikliği gerekiyorsa ilgili setup script'ini güncelle.
- Sahneler: `MainMenu`, `Facility_WakeUp` (açılış), `IndustrialYard_Night`, `SampleScene` (tırmanma/merdiven test alanı).
- Oyuncu: `Prefabs/Characters/Player.prefab` (CharacterController + `PlayerController`, `PlayerInventory`, `HeldItemVisual`, `PistolShot`, `LightBallThrow`, `InventoryCanvas` çocuğu). Animator: `Animations/Controllers/MaleAnimator.controller`, klipler `Animations/Player/` (Mixamo, `Remy@...` / `Male@...`).
- `PlayerController.cs` büyük dosya: hareket, traversal, merdiven foot IK. **Normal zıplama ve Jump Down yok**: Space sadece `LedgeMarker` olan yerlerde vault/tırmanma yapar, marker yoksa hiçbir şey olmaz. Engel algılama için ray yok (sadece iniş noktasının zeminini bulmak için kısa aşağı ray'ler var).
- `World/LedgeMarker.cs`: objenin üst yüzeyinin ortasına konan görünmez marker (gizmo: yeşil Vault, mavi Climb Up, turuncu Climb Over). Üst yüzey `length` (local X, uzun kenar) × `depth` (local Z). Vault ve Climb Over sadece local Z boyunca (iki yönden) geçilir; Climb Up dört kenardan da tutulur. Seçim: karakterin baktığı yönde (±60°), 1.1m içindeki, en fazla 2× boy yükseklikteki en yakın kenar.
  - `Vault` → "Vault Over" state (XZ yerinde oynar, transform kodla taşınır)
  - `ClimbUp` → bel boyu (≤0.65×) ise direkt "Climb Over" state (= Braced Hang To Crouch), daha yüksekse "Hang Up" (= Idle To Braced Hang) → "Climb Over". Dikkat: animator'daki "Climb Over" state adı aslında üstüne çıkma klibi.
  - `ClimbOver` → `ClimbUp` gibi tepeye çıkar, "Climb Over" %88'de "Hard Landing" (Mixamo Hard Landing, XZ yerinde) ile karşı tarafa düşer (`TraversalKind.Drop`). Çit (Concrete_fence_v2_S) bunu kullanır.
  - Marker'lar `Tools/Setup Ledge Markers` ile `Assets/Imported/Industrial` prop prefab'larına eklenir (liste script'te). Animator state'leri `Tools/Setup Ledge Traversal` ile kurulur; hepsi tag `Action`. Warp'lar kemik pozisyonlarından LateUpdate'te yapılır (eller kenara sabitlenir).
  - Karakterin gövde yönünü kamera sürüyor; testte rotasyonu traversal çağrısıyla aynı karede ver.
- Kamera: Cinemachine Third Person Follow (`Prefabs/Cameras/Cinemachine Cameras.prefab`), hedef Player altındaki "Third Person Aim" pivot'u (`CinemachineThirdPersonLook`). Traversal sırasında pivot kalçanın yüksekliğini takip eder. Layer 8 `CameraIgnore`: içinden görülen parçalar (çit teli) kamera çarpışmasına girmez, fizik aynı kalır; `Tools/Setup Camera Ignore Layer`.
- Editor script'lerinde `AssetDatabase.SaveAssets()` kullanıcının kaydedilmemiş değişikliklerini de yazar; mümkünse sadece ilgili asset'i kaydet.
- Input: Input System, `Input/PlayerInputActions.inputactions` (`Move`, `Sprint`, `Look`, `Jump`, `Attack`, `Interact`).
- Runtime UI genelde kodla kurulur (örn. `AmmoHud`, `CrosshairSpread`); hotbar/crosshair `InventoryCanvas` altında.
- Ses `Resources/Audio/` altından `Resources.Load` ile yüklenir; ses seviyeleri `AmbienceSettings`.

## Kod tarzı

- Mevcut stile uy: `[SerializeField] private` alanlar, `[Header]` grupları, `Debug.Log("[SınıfAdı] ...")` önekleri, kısa "neden" yorumları, sınıf başına `/// <summary>`.
- `GetComponent` / `FindAnyObjectByType` ile tembel referans bulma yaygın; yeni bağımlılıkta aynı kalıbı kullan.

## Unity Editor ile çalışma

- Editor genelde açık; `unity status` ile kontrol et, `unity command recompile` → `unity command recompile_status` ile derle, `unity command console` ile hataları oku.
- Editor açıkken `.unity` / `.prefab` / `.asset` YAML'ını elle düzenleme; `unity command eval` veya setup script'leri kullan.
- Yeni animasyon klipleri kullanıcı tarafından Mixamo'dan indirilir; klip yoksa uydurma, sor.
