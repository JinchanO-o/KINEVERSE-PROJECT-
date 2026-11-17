# KINEVERSE (NCKH_UTT)

# Hướng dẫn sử dụng
**Lưu ý:** Phải tải Editor version: 6000.5.6f1, nếu chưa tải mà vẫn vào file thì nó sẽ thay đổi cấu trúc file và không được commit thẳng lên `main` của Github.

- Vào Assets/Scenes/ nhấn đúp vào Main thì sẽ đến được Không gian làm việc.
- Prefabs: thư mục này chứa các object đã đóng gói sẵn. Kéo prefab từ Project vào Hierarchy (hoặc thả thẳng vào Scene view) là object xuất hiện.
- Model 3d: chứa các file model thô (.fbx, .obj...). Cũng kéo vào Hierarchy tương tự.
- Scripts: chứa code C#, không hiển thị ra scene. Script chỉ chạy khi được gắn vào một object.

- Clone the project: git clone https://github.com/JinchanO-o/NCKH_UTT.git
- Update the Lastest: git pull origin main 
# Giải thích các thư mục

## Model 3d folder
- ki: Mô hình các mạch.
- Bridge: Mô hình cầu đường.
- 组_1: 1 mặt phẳng lớn.
## Prefabs
- Item: Là các item có thể cầm nắm.
- Player chính là bàn tay của người chơi.

## "..\Assets\Editor\LabSceneBuilder.cs"
<details>
<summary>LabSceneBuilder.cs: Tool Unity Editor tự động cấu hình URP, khởi tạo Prefab/Material và dựng trọn gói Scene Lab 3D (phòng, bàn, đèn, vật phẩm, Player FPS) chỉ bằng 1-click menu.</summary>

### 1. Tổng quan
`LabSceneBuilder.cs` là một **Unity Editor Script** (chỉ chạy trong môi trường Editor, không đóng gói vào bản build). File này đóng vai trò là công cụ dựng toàn bộ môi trường thử nghiệm (prototype) tự động bằng mã nguồn (**Procedural Scene & Asset Generation**) thông qua thanh menu của Unity thay vì thao tác kéo-thả thủ công.

#### Cách vận hành công cụ (từ tài liệu trong code)
1. Chạy `Tools > Lab > Use URP` (chạy 1 lần để cấu hình Render Pipeline).
2. Chạy `Tools > Lab > Build Main Scene` để tự động sinh toàn bộ thư mục, vật liệu, Prefab và lưu Scene chính tại `Assets/Scenes/Main.unity` (chạy lại sẽ ghi đè dữ liệu cũ).

---

### 2. Công nghệ và kỹ thuật sử dụng

#### A. Hệ thống đồ họa Universal Render Pipeline (URP) & Custom Shader
* **Thư viện:** `UnityEngine.Rendering` và `UnityEngine.Rendering.Universal`.
* **Tự động chuyển đổi Pipeline (`UseUrp`):** Tự động tìm hoặc tạo `UniversalRenderPipelineAsset` trong `Assets/Settings`, sau đó gán vào cả `GraphicsSettings.defaultRenderPipeline` và `QualitySettings.renderPipeline` để khắc phục lỗi vật liệu bị hồng (magenta) do dự án khởi tạo bằng Built-in Render Pipeline.
* **Vật liệu & Post-Processing:** Sử dụng shader chuẩn `Universal Render Pipeline/Lit` (thông qua thuộc tính `_BaseColor`) và bật Post-Processing trên Camera thông qua component `UniversalAdditionalCameraData` (`renderPostProcessing = true`).
* **Custom Outline Shader:** Tích hợp shader tùy chỉnh `Lab/InvertedHullOutline` (kỹ thuật tạo viền đối tượng bằng phương pháp đảo ngược mặt - Inverted Hull, điều khiển màu qua thuộc tính `_OutlineColor`).

#### B. Unity Editor Scripting & Tự động hóa Asset Pipeline
* **Mở rộng Menu Editor (`[MenuItem]`):** Tạo 2 lệnh trực tiếp trên thanh công cụ: `Tools/Lab/Use URP` và `Tools/Lab/Build Main Scene`.
* **Quản lý tài nguyên (`AssetDatabase` & `PrefabUtility`):**
  * Tự động kiểm tra và tạo cấu trúc thư mục: `Assets/Settings`, `Assets/Scenes`, `Assets/Prefabs`, `Assets/Materials`.
  * Khởi tạo và lưu trữ 12 file vật liệu (`.mat`).
  * Đóng gói Prefab (`Item.prefab`, `Player.prefab`) bằng `PrefabUtility.SaveAsPrefabAsset` và khởi tạo bản thể trong scene bằng `PrefabUtility.InstantiatePrefab`.
* **Quản lý Scene & Build (`EditorSceneManager` & `EditorBuildSettings`):** Khởi tạo scene trống (`NewSceneSetup.EmptyScene`), lưu thành `Assets/Scenes/Main.unity` và tự động thêm scene này vào danh sách Build Settings.
* **Can thiệp cấu hình Project (`SerializedObject`):** Đọc và ghi trực tiếp vào `ProjectSettings/TagManager.asset` để tự động khai báo **Layer 6 (`Player`)** và **Layer 7 (`Pickable`)**.

#### C. Cấu trúc Nhân vật Góc nhìn thứ nhất (First-Person) & Tương tác Vật lý
* **Điều khiển nhân vật:** Sử dụng `CharacterController` (`height = 1.8f`, `radius = 0.3f`, `center = (0, 0.9, 0)`).
* **Hệ thống Camera & Tay nhân vật (First-Person Rig):**
  * Tổ chức theo cây phân cấp: `Player` $\rightarrow$ `Head` (cao `1.6m`) $\rightarrow$ `Camera` (tag `MainCamera`, `FOV = 75`, `nearClipPlane = 0.05`) $\rightarrow$ `Hand Rig`.
  * `Hand Rig` hiện tại sử dụng 2 khối nguyên thủy (`Palm` và `Forearm`) đã xóa Collider để làm mô hình tay giữ chỗ (placeholder).
* **Vật thể tương tác (`Pickable`):** Tạo từ `PrimitiveType.Cube`, gán Layer `Pickable` và truyền thông số khối lượng (`mass` từ `0.5f` đến `8.0f`) vào component `Pickable`. Các vật thể trên bàn đang được đặt lơ lửng trong không gian.

#### D. Chiếu sáng & Môi trường (Lighting & Environment)
* **Hệ thống đèn:** Sử dụng 3 đèn `LightType.Spot` (Spotlight) gắn trần với cường độ tính theo đơn vị vật lý candela (`350f`), góc chiếu `95°`, tầm chiếu `9m` và đổ bóng mềm (`LightShadows.Soft`). Đèn ở giữa (`Light B`) được gắn thêm hiệu ứng nhấp nháy.
* **Môi trường (`RenderSettings`):** Thiết lập ánh sáng môi trường phẳng (`AmbientMode.Flat`) tông tối và hiệu ứng sương mù hàm mũ (`FogMode.Exponential`, mật độ `0.03f`).

---

### 3. Các thành phần phụ thuộc (Dependencies)
`LabSceneBuilder.cs` khởi tạo và gán tham chiếu tới **7 script runtime** và **1 file shader** khác trong dự án (chưa có mã nguồn trong file này):

1. `PlayerController.cs` — Xử lý di chuyển và xoay góc nhìn của nhân vật.
2. `PlayerInteractor.cs` — Xử lý cơ chế tương tác, cầm/nắm vật thể (`Pickable`) và điều khiển vị trí `Hand Rig` (`restPose`).
3. `Pickable.cs` — Định nghĩa thuộc tính vật thể có thể nhặt (lưu trữ biến `mass`).
4. `HighlightManager.cs` — Quản lý hiệu ứng làm nổi bật vật thể khi ngắm vào (sử dụng `outlineMaterial`).
5. `HUD.cs` — Hiển thị giao diện người dùng trên màn hình (nhận tham chiếu từ `PlayerInteractor`).
6. `RoomAmbience.cs` — Quản lý âm thanh hoặc không khí môi trường trong phòng thí nghiệm.
7. `FlickerLight.cs` — Tạo hiệu ứng nhấp nháy cho đèn trần `Light B`.
8. `InvertedHullOutline.shader` — Shader tạo viền ngoài cho vật thể được chọn.
</details>


## "..\Assets\Scripts\Audio\AudioGenerator.cs"
<details>
<summary>AudioGenerator.cs: Script tĩnh tự động tổng hợp âm thanh (Procedural Audio) bằng thuật toán toán học, tạo các AudioClip tiếng bước chân, cầm/thả đồ, va chạm, tiếng đèn điện và âm nền phòng mà không cần file audio bên ngoài.</summary>

### 1. Tổng quan
`AudioGenerator.cs` là một lớp tĩnh C# (`public static class AudioGenerator`) đóng vai trò là một **bộ tổng hợp âm thanh bằng thuật toán (Procedural Sound Synthesizer / DSP)**. 

Script này tự động tính toán dữ liệu sóng âm (PCM Waveform) ở tần số chuẩn **44.1kHz** và tạo các đối tượng `AudioClip` ngay trong bộ nhớ. Điều này giúp dự án có sẵn các âm thanh phản hồi (placeholder sound effects) để sử dụng ngay mà không cần tải hay đính kèm bất kỳ file audio tĩnh nào (`.wav`, `.mp3`).

---

### 2. Các hiệu ứng âm thanh được tổng hợp (`AudioClip`)

* **`Footstep()` (Tiếng bước chân - 0.2s):** 
  * Kết hợp White Noise (nhiễu trắng) giảm dần cực nhanh ($e^{-45t}$) và sóng nén tần số thấp **95Hz** ($e^{-22t}$) để giả lập tiếng bước chân giậm xuống mặt sàn.
* **`Grab()` (Tiếng nhặt / cầm vật phẩm - 0.15s):** 
  * Sóng Sine với tần số quét tăng dần từ **600Hz lên 3100Hz** kết hợp suy hao biên độ nhanh ($e^{-35t}$), tạo ra tiếng "click" nhấc đồ mượt mà.
* **`Release()` (Tiếng thả / đặt vật phẩm - 0.25s):** 
  * Sóng Sine với tần số quét giảm dần từ **500Hz xuống** kết hợp một chút nhiễu không khí ($e^{-60t}$) để tạo tiếng thả đồ nhẹ nhàng.
* **`Collision()` (Tiếng va chạm / rơi đồ - 0.35s):** 
  * Âm trầm "thịch" kết hợp sóng Sine **110Hz** ($e^{-14t}$) và tiếng ồn va đập ngẫu nhiên ($e^{-30t}$) để giả lập tiếng vật thể chạm sàn/bàn.
* **`Buzz()` (Tiếng điện kêu từ đèn nhấp nháy - 0.4s):** 
  * Tổng hợp sóng Sine tần số dòng điện **120Hz** kết hợp nhiễu ngẫu nhiên và điều chế biên độ (Amplitude Modulation) tần số **18Hz** để tạo tiếng dế điện kêu của bóng đèn lỗi.
* **`RoomTone()` (Âm nền phòng thí nghiệm - Vòng lặp 4s):** 
  * Giả lập tiếng quạt thông gió trầm (Brown Noise) kết hợp tiếng ù lưới điện tần số **60Hz** và hài bậc hai **120Hz**. 
  * Áp dụng kỹ thuật **Crossfade 0.25s** nối vùng cuối mảng vào vùng đầu bằng `Mathf.Lerp` để âm thanh lặp lại liên tục mượt mà không bị vấp (Seamless Loop).

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Xử lý Tín hiệu Số & Tổng hợp Âm thanh (Digital Signal Processing - DSP)
* **Phương trình toán học sóng âm:** Sử dụng hàm lượng giác `Mathf.Sin` (tạo dao động tần số), hàm mũ `Mathf.Exp` (tạo độ suy hao âm lượng theo thời gian - Envelope/Decay), và `Random.value` (tạo nhiễu - Noise Generator).
* **Quét tần số (Frequency Sweep / FM Synthesis):** Điều khiển tần số biến thiên theo thời gian $f(t) = a\cdot t + b\cdot t^2$ để tạo hiệu ứng âm thanh trượt cao độ (pitch sweep) như tiếng Grab/Release.

#### B. Unity Audio API (`AudioClip.Create` & `SetData`)
* Tự động khởi tạo cấu trúc `AudioClip` trong bộ nhớ thông qua `AudioClip.Create(name, lengthSamples, channels, frequency, stream)`.
* Đổ trực tiếp mảng dữ liệu mẫu `float[]` (giá trị chuẩn hóa từ `-1.0f` đến `1.0f` thông qua `Mathf.Clamp`) vào `AudioClip` bằng phương thức `SetData()`.

#### C. Kiến trúc & Kỹ thuật lập trình (Design Patterns)
* **Caching Pattern (Bộ nhớ tạm):** Lưu trữ các biến `static AudioClip` (`_footstep`, `_grab`, `_release`, ...). Mẫu âm thanh chỉ được tính toán và tạo 1 lần duy nhất ở lần gọi đầu tiên (Lazy Initialization), các lần sau sẽ tái sử dụng ngay lập tức mà không tiêu tốn CPU.
* **Seamless Loop Crossfading:** Thuật toán trộn tín hiệu âm thanh ở 0.25 giây cuối file vào đoạn đầu file giúp loại bỏ hiện tượng bị giật/khựng tần số khi phát lặp lại (Looping).
</details>

## "InputBridge.cs"
<details>
<summary>InputBridge.cs: Lớp tĩnh trừu tượng hóa hệ thống nhập liệu (Input Abstraction Layer), tự động chuyển đổi giữa Unity Input System mới và Legacy Input Manager để cung cấp tín hiệu điều khiển di chuyển, góc nhìn và tương tác.</summary>

### 1. Tổng quan
`InputBridge.cs` là một lớp tĩnh C# (`public static class InputBridge`) đóng vai trò là **lớp trung gian nhập liệu (Input Wrapper)** giúp các script gameplay (`PlayerController`, `PlayerInteractor`) không phụ thuộc trực tiếp vào một backend input cụ thể.

Script sử dụng chỉ thị tiền biên dịch để tự động nhận diện và sử dụng gói **Unity Input System** mới nếu được kích hoạt, hoặc tự động quay về dùng **Input Manager** truyền thống của Unity.

---

### 2. Các chức năng & Phím điều khiển được ánh xạ

- **`Move()` (Di chuyển - `Vector2`):** 
  * Đọc tín hiệu từ cụm phím **WASD** (trên Input System mới) hoặc trục `"Horizontal"` / `"Vertical"` (trên Input Manager cũ) để trả về vector hướng di chuyển 2 chiều.
- **`Look()` (Xoay góc nhìn - `Vector2`):** 
  * Trả về độ lệch tọa độ chuột trong frame hiện tại (`Mouse.current.delta` tính theo pixel hoặc trục `"Mouse X"` / `"Mouse Y"`).
- **`Sprint()` & `Crouch()` (Trạng thái chạy nhanh & ngồi - `bool`):** 
  * Kiểm tra trạng thái giữ phím **Left Shift** (chạy nhanh) và **Left Ctrl** (ngồi/cúi người).
- **`EscapePressed()` (Tạm dừng - `bool`):** 
  * Kiểm tra sự kiện nhấn phím **Esc** trong frame hiện tại (`wasPressedThisFrame` / `GetKeyDown`) để bật/tắt menu tạm dừng.
- **`GrabPressed()` & `GrabHeld()` (Cầm & giữ vật phẩm - `bool`):** 
  * Kiểm tra sự kiện nhấn xuống và trạng thái giữ **Chuột trái (LMB)** để bắt đầu nhặt và duy trì việc giữ vật thể.
- **`HighlightPressed()` (Đánh dấu vật phẩm - `bool`):** 
  * Kiểm tra sự kiện nhấn **Chuột phải (RMB)** trong frame để bật/tắt viền highlight cho đối tượng đang ngắm.
- **`Wheel()` (Điều chỉnh khoảng cách cầm - `float`):** 
  * Đọc giá trị cuộn con lăn chuột (chuẩn hóa chia `120f` trên Input System mới) và giới hạn giá trị trả về trong khoảng `[-3f, 3f]` bằng `Mathf.Clamp`.

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Biên dịch có điều kiện (Conditional Compilation)
- Sử dụng cặp chỉ thị `#if ENABLE_INPUT_SYSTEM ... #else ... #endif` để tách biệt mã nguồn ngay tại thời điểm biên dịch, giúp dự án không bị lỗi thiếu namespace nếu chuyển đổi cấu hình Active Input Handling trong Project Settings.

#### B. Unity Input APIs
- Kết hợp song song thư viện `UnityEngine.InputSystem` (`Keyboard.current`, `Mouse.current`) và lớp `UnityEngine.Input` truyền thống.

#### C. Kiến trúc & Kỹ thuật lập trình (Design Patterns)
- **Adapter / Facade Pattern:** Đóng gói các lệnh đọc thiết bị phần cứng thô thành các phương thức tĩnh mang ngữ nghĩa gameplay rõ ràng (`GrabPressed()`, `HighlightPressed()`), giúp dễ dàng thay đổi phím bấm tại một nơi duy nhất.
</details>

## "FlickerLight.cs"
<details>
<summary>FlickerLight.cs: Component điều khiển hiệu ứng đèn trần nhấp nháy ngẫu nhiên theo chu kỳ thời gian và phát âm thanh rè điện 3D tại vị trí nguồn sáng.</summary>

### 1. Tổng quan
`FlickerLight.cs` (`public class FlickerLight : MonoBehaviour`) là component mô phỏng bóng đèn trần bị chập chờn trong phòng thí nghiệm. Script bắt buộc đối tượng gắn phải có component `Light` thông qua thuộc tính `[RequireComponent(typeof(Light))]`.

Script lưu lại cường độ sáng ban đầu của đèn (`_light.intensity`) làm mốc chuẩn và luân phiên điều khiển đèn giữa trạng thái hoạt động ổn định (có dao động nhẹ) với các đợt nhấp nháy ngẫu nhiên kèm tiếng điện kêu.

---

### 2. Các chức năng và luồng hoạt động

- **Khởi tạo (`Awake`):**
  * Lấy tham chiếu `Light`, lưu giá trị cường độ gốc vào `_baseIntensity`, nạp mẫu âm thanh rè điện từ `AudioGenerator.Buzz()` và lên lịch thời điểm nhấp nháy đầu tiên qua `ScheduleNext()`.
- **Trạng thái ổn định (`Update`):**
  * Ngừng cập nhật khi game đang tạm dừng (`HUD.IsPaused == true`).
  * Khi chưa tới đợt nhấp nháy, cường độ sáng dao động rất nhẹ theo sóng sin (`_baseIntensity * (1f + 0.01f * Mathf.Sin(Time.time * 7f))`) để nguồn sáng trông tự nhiên thay vì đứng yên tuyệt đối.
- **Kích hoạt & Xử lý nhấp nháy (`StartFlicker`):**
  * Khi `Time.time >= _nextEvent`, đèn chuyển sang trạng thái `_flickering` kéo dài trong khoảng `flickerDuration` (mặc định `0.15s` đến `0.6s`) và phát âm thanh rè điện 3D tại vị trí của đèn nếu `playSound` được bật.
  * Trong suốt thời gian nhấp nháy, mỗi frame có **40% xác suất** (`Random.value < 0.4f`) đèn sụt sáng mạnh xuống mức `5% - 40%` cường độ gốc, và **60% xác suất** dao động ở mức `80% - 115%`. Khi hết thời gian, đèn phục hồi về `_baseIntensity` và hẹn lịch đợt tiếp theo trong khoảng `intervalRange` (`4s` đến `10s`).

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Unity Lighting & Spatial Audio API
- Can thiệp trực tiếp thuộc tính `Light.intensity` theo thời gian thực.
- Sử dụng `AudioSource.PlayClipAtPoint(_buzz, transform.position, soundVolume)` để phát âm thanh một lần (one-shot) trong không gian 3D tại đúng tọa độ bóng đèn mà không cần gắn sẵn `AudioSource`.

#### B. Lập lịch sự kiện bằng bộ đếm thời gian (Time-based Scheduling)
- Sử dụng mốc thời gian `Time.time` kết hợp hàm ngẫu nhiên `Random.Range` và `Random.value` để quản lý máy trạng thái (ổn định / nhấp nháy) trực tiếp trong `Update()`.
</details>

## "RoomAmbience.cs"
<details>
<summary>RoomAmbience.cs: Component kiến tạo bầu không khí phòng thí nghiệm bằng cách phát vòng lặp âm nền 2D và sinh hệ thống hạt bụi (Particle System) lơ lửng trong không gian.</summary>

### 1. Tổng quan
`RoomAmbience.cs` (`public class RoomAmbience : MonoBehaviour`) được gắn vào đối tượng gốc của phòng thí nghiệm (`Lab`) nhằm tạo chiều sâu không gian về cả thính giác lẫn thị giác ngay khi scene khởi chạy.

Script tự động thiết lập nguồn phát âm thanh nền liên tục và khởi tạo hoàn toàn bằng mã nguồn một hệ thống hạt bụi (`Dust Motes`) bay lơ lửng dưới ánh đèn trần.

---

### 2. Các thành phần được khởi tạo

- **Âm thanh nền phòng thí nghiệm (`AudioSource`):**
  * Tự động thêm component `AudioSource` vào đối tượng gốc, gán clip từ `AudioGenerator.RoomTone()`, bật chế độ lặp (`loop = true`), thiết lập âm thanh 2D toàn không gian (`spatialBlend = 0f`) với âm lượng `toneVolume` (`0.3f`) và phát ngay trong `Awake()`.
- **Hệ thống hạt bụi không khí (`CreateDust()`):**
  * Tạo GameObject con tên `"Dust Motes"` tại tọa độ `roomCentre` (`0, 1.5, 0`) và gắn `ParticleSystem`.
  * **Thông số hạt (`main` & `emission`):** Phát liên tục `12` hạt/giây (`dustRate`), tối đa `300` hạt tồn tại đồng thời, vòng đời mỗi hạt từ `8s - 14s`, kích thước siêu nhỏ (`0.008m - 0.02m`), tốc độ trôi chậm (`0.005 - 0.03 m/s`) với màu vàng nhạt bán trong suốt (`RGBA: 1, 0.95, 0.85, 0.35`).
  * **Vùng phát & Chuyển động (`shape` & `noise`):** Phát ngẫu nhiên bên trong thể tích hình hộp `roomSize` (`9.6m x 2.8m x 7.6m`) theo không gian thế giới (`World`), kết hợp module `noise` (`strength = 0.03f`, `frequency = 0.3f`) để hạt bụi bay lượn tự nhiên.

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Runtime Particle System Configuration
- Khởi tạo và cấu hình toàn bộ các module của `ParticleSystem` (`main`, `emission`, `shape`, `noise`, `ParticleSystemRenderer`) trực tiếp qua C# API mà không cần tạo sẵn Prefab.

#### B. Cấu hình Shader & Vật liệu trong suốt trên URP
- Tự động tìm kiếm shader `Universal Render Pipeline/Particles/Unlit` (có phương án dự phòng `Particles/Standard Unlit`) và khởi tạo `Material` mới.
- Thiết lập chế độ hòa trộn trong suốt (`_Surface = 1f`, bật keyword `_SURFACE_TYPE_TRANSPARENT`, ghi đè tag `"RenderType" = "Transparent"`, đặt `renderQueue` ở mức `RenderQueue.Transparent`) để các hạt bụi mờ mịn thay vì hiển thị dạng khối vuông đặc.
</details>

## "HighlightManager.cs"
<details>
<summary>HighlightManager.cs: Singleton quản lý trạng thái đánh dấu viền (Outline Highlight) cho duy nhất một vật thể tương tác tại một thời điểm và đồng bộ màu sắc với shader viền.</summary>

### 1. Tổng quan
`HighlightManager.cs` (`public class HighlightManager : MonoBehaviour`) chịu trách nhiệm điều phối hiệu ứng viền sáng xung quanh vật phẩm khi người chơi nhấn chuột phải.

Hệ thống đảm bảo tại mọi thời điểm chỉ có tối đa một đối tượng `Pickable` được đánh dấu (`Current`), đồng thời phát sự kiện thông báo mỗi khi trạng thái đánh dấu thay đổi.

---

### 2. Các chức năng và thành phần chính

- **`Instance` & `Current`:**
  * `Instance`: Thuộc tính Singleton tĩnh cho phép truy cập từ mọi nơi (như `PlayerInteractor`, `Pickable`, `HUD`).
  * `Current`: Lưu trữ tham chiếu tới đối tượng `Pickable` đang được đánh dấu hiện tại (hoặc `null` nếu không có vật nào).
- **`HighlightChanged` (Sự kiện thay đổi):**
  * Khai báo `public event Action<Pickable> HighlightChanged`, kích hoạt mỗi khi đối tượng được đánh dấu thay đổi để các thành phần khác cập nhật giao diện.
- **`Toggle(Pickable item)` & `SetHighlight(Pickable item)`:**
  * `Toggle`: Xử lý hành vi bật/tắt khi nhấn chuột phải — nếu `item` đang được đánh dấu thì tắt đi (`SetHighlight(null)`), ngược lại thì chuyển viền sáng sang `item`.
  * `SetHighlight`: Tắt hiệu ứng viền ở vật phẩm cũ (`Current.SetHighlighted(false)`), gán `Current = item`, bật hiệu ứng viền ở vật phẩm mới và kích hoạt sự kiện `HighlightChanged`.
- **`ApplyColor()`:**
  * Đồng bộ thuộc tính màu `outlineColor` (mặc định màu cam `#FF8800`) vào biến `_OutlineColor` của `outlineMaterial` trong cả `Awake()` và `OnValidate()`.

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Design Patterns (Singleton & Observer)
- **Singleton Pattern:** Triển khai `public static HighlightManager Instance` và giải phóng tham chiếu trong `OnDestroy()` (`if (Instance == this) Instance = null`) để tránh rò rỉ bộ nhớ khi tải lại scene.
- **Observer Pattern:** Sử dụng C# `event Action<Pickable>` để tách biệt hoàn toàn logic quản lý highlight khỏi lớp hiển thị UI/HUD.

#### B. Shader Property Manipulation
- Cập nhật trực tiếp tham số `_OutlineColor` của vật liệu sử dụng shader tùy chỉnh `Lab/InvertedHullOutline` cả trong lúc chỉnh sửa trên Editor (`OnValidate`) lẫn khi chạy game (`Awake`).
</details>

## "Pickable.cs"
<details>
<summary>Pickable.cs: Component định nghĩa vật thể có thể cầm nắm trong môi trường, quản lý khối lượng, tốc độ trễ khi kéo, âm thanh tương tác, sự kiện va chạm và dựng lưới viền Inverted Hull.</summary>

### 1. Tổng quan
`Pickable.cs` (`public class Pickable : MonoBehaviour`) biến một đối tượng 3D có `Collider` (`[RequireComponent(typeof(Collider))]`) thành vật phẩm tương tác.

Đặc điểm kiến trúc của dự án này là vật phẩm **không sử dụng `Rigidbody` hay trọng lực**; thay vào đó `PlayerInteractor` di chuyển vật thể bằng thuật toán quét hình cầu (Sphere Sweep), giúp vật thể giữ nguyên vị trí lơ lửng tại nơi được thả ra.

---

### 2. Các chức năng và thành phần chính

- **Quản lý danh sách & Layer:**
  * Tự động gán layer của GameObject thành `"Pickable"` trong `Awake()` và tự động thêm/xóa bản thân khỏi danh sách tĩnh `Pickable.All` thông qua `OnEnable()` và `OnDisable()`.
- **Tính toán độ trễ theo khối lượng & Bán kính va chạm:**
  * `FollowSpeed`: Nội suy tuyến tính tốc độ bám theo tay cầm dựa trên thông số `mass` (từ `0.5` đến `10`). Vật nhẹ di chuyển nhanh (`lightFollowSpeed = 18f`), vật nặng có độ trễ lớn (`heavyFollowSpeed = 4f`).
  * `Radius`: Tự động tính bán kính quét va chạm bằng một nửa cạnh nhỏ nhất của `Collider.bounds.extents` (tối thiểu `0.01f`).
- **Sự kiện tương tác & Âm thanh 3D (`NotifyGrabbed`, `NotifyReleased`, `NotifyCollided`):**
  * Cập nhật trạng thái `IsHeld`, kích hoạt các sự kiện `Grabbed`, `Released`, `Collided` và phát âm thanh 3D tại vị trí vật thể qua `AudioSource.PlayClipAtPoint` (tự động lấy clip từ `AudioGenerator` nếu chưa gán sẵn).
  * Với `NotifyCollided(float impactSpeed)`, chỉ kích hoạt khi vận tốc va chạm $\ge 0.1\text{ m/s}$ và điều chỉnh âm lượng từ `0.2f` đến `1f` tỉ lệ thuận với lực va đập (`impactSpeed / 3f`).
- **Tạo viền đối tượng (`SetHighlighted` & `EnsureOutline`):**
  * Khởi tạo động một GameObject con tên `"Outline"` thuộc layer `"Ignore Raycast"`, sao chép `sharedMesh` từ `MeshFilter` của vật thể gốc và gắn `outlineMaterial` (tắt đổ bóng và nhận bóng).

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Inverted Hull Outline Mesh Generation
- Tạo bản sao lưới (Mesh) dạng đối tượng con chỉ khi người chơi lần đầu bật highlight (Lazy Instantiation) và đặt ở layer `"Ignore Raycast"` để lưới viền mở rộng không làm sai lệch tia raycast nhắm mục tiêu.

#### B. Kiến trúc & Kỹ thuật lập trình (Design Patterns)
- **Registry Pattern:** Duy trì `public static readonly List<Pickable> All` để theo dõi toàn bộ vật thể tương tác đang hoạt động trong scene.
- **Decoupled Event-Driven Architecture:** Sử dụng bộ ba `Action` events (`Grabbed`, `Released`, `Collided`) để tách biệt dữ liệu vật thể khỏi hiệu ứng âm thanh, rung camera và HUD.
</details>

## "PlayerController.cs"
<details>
<summary>PlayerController.cs: Bộ điều khiển nhân vật góc nhìn thứ nhất (First-Person Controller) xử lý xoay góc nhìn mượt, di chuyển có gia tốc, tư thế đứng/ngồi, hiệu ứng camera (Head Bob, FOV, Sway, Shake) và tiếng bước chân đồng bộ quãng đường.</summary>

### 1. Tổng quan
`PlayerController.cs` (`public class PlayerController : MonoBehaviour`) yêu cầu component `CharacterController` và quản lý toàn bộ chuyển động vật lý cùng cảm giác góc nhìn của người chơi.

Script hoạt động theo cấu trúc phân cấp `Player` $\rightarrow$ `Head` $\rightarrow$ `Camera`, tự động loại bỏ layer `"Player"` và `"Pickable"` khỏi `obstacleMask`, đồng thời khóa con trỏ chuột (`CursorLockMode.Locked`) khi bắt đầu.

---

### 2. Các phân hệ chức năng chính

- **Điều khiển góc nhìn (`HandleLook`):**
  * Đọc độ lệch chuột từ `InputBridge.Look()`, giới hạn góc ngẩng/cúi trong khoảng $[-85^\circ, 85^\circ]$ (`maxPitch`) và làm mượt chuyển động xoay bằng nội suy mũ (`1f - Mathf.Exp(-lookSmoothing * dt)`).
- **Tư thế đứng & ngồi (`HandleStance`):**
  * Chuyển đổi mượt chiều cao `CharacterController` (`1.8m` xuống `1.1m`) và độ cao `head` (`1.6m` xuống `0.95m`).
  * Tích hợp kiểm tra vật cản phía trên đầu bằng `Physics.CheckSphere` (bán kính `0.25f`) để giữ nhân vật ở tư thế ngồi nếu phía trên không đủ khoảng trống đứng dậy.
- **Di chuyển có quán tính (`HandleMovement`):**
  * Tính toán vận tốc ngang với gia tốc (`acceleration = 14f`) khi nhấn phím và giảm tốc (`deceleration = 10f`) khi thả phím thay vì dừng đột ngột.
  * Hỗ trợ chạy nhanh (`sprintSpeed = 5m/s`, chỉ kích hoạt khi đi tới và không ngồi) và đi ngồi (`crouchSpeed = 1.3m/s`), áp dụng trọng lực (`gravity = 24f`) và xử lý va chạm qua `_cc.Move()`.
- **Hiệu ứng Camera (`HandleCameraFeel`):**
  * Tổng hợp 4 lớp chuyển động cho camera: nhấp nhô bước đi theo hàm lượng giác (`Head Bob`), đung đưa nhẹ khi đang cầm vật phẩm (`IsHoldingItem`), rung chấn (`AddShake` suy giảm theo thời gian) và mở rộng góc nhìn FOV ($75^\circ$ lên $85^\circ$) khi chạy nhanh.
- **Tiếng bước chân theo sải chân (`HandleFootsteps`):**
  * Tích lũy quãng đường thực tế đã di chuyển (`_stepDistance`); mỗi khi đạt ngưỡng sải chân (`walkStride = 1.2m`, `sprintStride = 1.7m`, `crouchStride = 0.75m`), phát âm thanh 2D (`spatialBlend = 0f`) với cao độ (`pitch`) ngẫu nhiên (`0.9 - 1.1`) và âm lượng thay đổi theo tư thế.

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Nội suy làm mượt độc lập tốc độ khung hình (Frame-rate Independent Smoothing)
- Áp dụng công thức suy giảm mũ $k = 1 - e^{-\lambda \cdot \Delta t}$ cho xoay camera, chuyển đổi chiều cao tư thế và chuyển đổi FOV để đảm bảo cảm giác điều khiển nhất quán ở mọi mức FPS.

#### B. Unity CharacterController & Physics Queries
- Kết hợp `CharacterController.Move` để trượt dọc theo tường/bàn và `Physics.CheckSphere` với `QueryTriggerInteraction.Ignore` để kiểm tra khoảng sáng trần khi đứng dậy.

#### C. Procedural Camera Kinematics
- Kết hợp dao động sóng sin/cos đa tần số (Sway, Head Bob) và thuật toán Trauma-based Camera Shake (biên độ rung tỉ lệ với $\text{shake}^2 \times 0.04$).
</details>

## "PlayerInteractor.cs"
<details>
<summary>PlayerInteractor.cs: Component xử lý tương tác vật thể góc nhìn thứ nhất gồm nhắm mục tiêu qua tâm màn hình, cầm/kéo vật phẩm có quét chống xuyên tường, điều chỉnh khoảng cách bằng con lăn chuột, đánh dấu viền và điều khiển chuyển động tay nhân vật.</summary>

### 1. Tổng quan
`PlayerInteractor.cs` (`public class PlayerInteractor : MonoBehaviour`) được gắn trên đối tượng `Player` bên cạnh `PlayerController`.

Script kết nối tín hiệu từ `InputBridge` với `Pickable`, `HighlightManager` và `PlayerController` để thực hiện cơ chế cầm nắm vật thể lơ lửng có độ trễ theo khối lượng và ngăn chặn vật thể đi xuyên qua tường hoặc mặt bàn.

---

### 2. Các cơ chế hoạt động chính

- **Nhận diện mục tiêu dưới tâm ngắm (`UpdateTarget`):**
  * Khi chưa cầm vật nào, bắn tia `Physics.Raycast` từ tâm camera trong tầm với `reach = 4m` (bỏ qua layer `Player`) để tìm component `Pickable`. Phát sự kiện `TargetChanged` mỗi khi đối tượng dưới tâm ngắm thay đổi.
- **Cầm, điều chỉnh khoảng cách & Thả vật phẩm (`BeginGrab`, `EndGrab`):**
  * Nhấn chuột trái để bắt đầu cầm (`Held`), giữ nguyên khoảng cách thực tế từ camera tới vật thể (kẹp trong khoảng `0.8m` đến `6m`).
  * Cuộn con lăn chuột để đẩy vật ra xa hoặc kéo lại gần theo từng nấc `wheelStep = 0.25m`. Khi thả chuột trái, vật thể đứng yên tại tọa độ hiện tại.
- **Di chuyển vật thể chống xuyên thấu 2 bước (`MoveHeldItem`):**
  * Tính điểm giữ vật phía trước camera (có độ lệch phải `0.18m` và xuống `0.12m`), sau đó thực hiện **2 pha quét va chạm cầu (`Physics.SphereCast`)**:
    1. **Pha 1 (Camera $\rightarrow$ Điểm giữ):** Ngăn điểm giữ lọt ra phía sau tường hoặc mặt bàn, giữ khoảng đệm `skin = 0.02m`.
    2. **Pha 2 (Vị trí hiện tại $\rightarrow$ Vị trí mới):** Nội suy vị trí vật thể theo `Held.FollowSpeed` (tạo độ trễ theo khối lượng), sau đó quét dọc đường đi để chặn vật thể xuyên qua bề mặt vật cản. Nếu va chạm xảy ra ở frame đầu tiên (`hitNow && !_blocked`), tính vận tốc va đập (`dist / dt`), gọi `Held.NotifyCollided` và kích hoạt rung camera qua `player.AddShake`.
- **Đánh dấu vật thể & Chuyển động tay (`UpdateHand`):**
  * Nhấn chuột phải gọi `HighlightManager.Instance.Toggle(Target)`.
  * Điều khiển đối tượng `hand` vươn tới tọa độ vật thể đang nhắm/cầm (nâng cao tối thiểu `y = -0.2f` khi đang giữ đồ) hoặc thu về tư thế nghỉ `restPose` (`0.28, -0.32, 0.5`).

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Dual-Pass SphereCast Collision Detection
- Sử dụng `Physics.SphereCast` hai pha kết hợp khoảng đệm bề mặt (`skin = 0.02f`) để chống xuyên vật thể (Anti-Tunneling) hoàn toàn bằng truy vấn vật lý động học mà không cần gắn `Rigidbody` lên vật phẩm.

#### B. Chuyển đổi hệ tọa độ (Coordinate Space Transformation)
- Sử dụng `playerCamera.transform.InverseTransformPoint` để chuyển tọa độ vật thể từ không gian thế giới (World Space) sang không gian cục bộ của camera (Camera Space), giới hạn biên độ bằng `Mathf.Clamp` để điều khiển cánh tay (`Hand Rig`) vươn theo mục tiêu tự nhiên.

#### C. Event-Driven Coupling
- Phát `TargetChanged` và cập nhật cờ `player.IsHoldingItem` để phối hợp nhịp nhàng với giao diện HUD và hiệu ứng đung đưa camera.
</details>

## "HUD.cs"
<details>
<summary>HUD.cs: Component tự động khởi tạo giao diện người dùng (Crosshair, Prompt ngữ cảnh, Menu tạm dừng) và hiệu ứng hậu kỳ URP (Vignette, Bokeh Depth of Field tự động lấy nét theo vật thể) hoàn toàn bằng mã nguồn lúc chạy.</summary>

### 1. Tổng quan

`HUD.cs` (`public class HUD : MonoBehaviour`) quản lý toàn bộ giao diện hiển thị trên màn hình (Screen UI), trạng thái tạm dừng toàn cục của trò chơi (`HUD.IsPaused`) và hệ thống hiệu ứng hậu kỳ (Post-Processing) trên Universal Render Pipeline (URP).

Điểm đặc biệt của script này là **không cần thiết kế sẵn Prefab Canvas, EventSystem hay Volume Profile trong Editor**; toàn bộ các thành phần UI và hậu kỳ đều được khởi tạo động bằng mã nguồn C# ngay trong `Awake()`.

---

### 2. Các chức năng và thành phần chính

* **Quản lý trạng thái tạm dừng toàn cục (`IsPaused` & `SetPaused`):**
  * Cung cấp thuộc tính tĩnh `public static bool IsPaused { get; private set; }` để các script khác (`PlayerController`, `PlayerInteractor`, `FlickerLight`) kiểm tra và ngừng nhận input hoặc cập nhật logic khi đang mở menu.
  * Tự động đặt lại trạng thái mặc định (`IsPaused = false`, `Time.timeScale = 1f`, `AudioListener.pause = false`) trong cả `Awake()` và `OnDestroy()` để tránh lỗi kẹt trạng thái tạm dừng khi tải lại scene.
  * Nhấn phím **Esc** (`InputBridge.EscapePressed()`) để bật/tắt menu tạm dừng: đóng băng thời gian (`Time.timeScale = 0f`), tắt toàn bộ âm thanh (`AudioListener.pause = true`), mở khóa và hiển thị con trỏ chuột (`CursorLockMode.None`).

* **Khởi tạo hệ thống giao diện động (`BuildEventSystem` & `BuildCanvas`):**
  * **`EventSystem`:** Tự động kiểm tra và tạo `EventSystem` nếu scene chưa có, tự động gắn `InputSystemUIInputModule` (nếu bật Input System mới) hoặc `StandaloneInputModule` (Input Manager cũ).
  * **`HUD Canvas`:** Tạo Canvas chế độ `ScreenSpaceOverlay` với `CanvasScaler` tự co giãn theo độ phân giải chuẩn `1920x1080` (`matchWidthOrHeight = 0.5f`).
  * **Tâm ngắm (`Crosshair`):** Dựng 4 thanh hình chữ nhật trắng mờ (`8x2 px` và `2x8 px`, alpha `0.85f`) đặt cách tâm màn hình `9px`.
  * **Hướng dẫn ngữ cảnh (`Prompt`):** Vùng văn bản phía dưới màn hình (`anchor: 0.5, 0.12`, font `LegacyRuntime.ttf`, cỡ `26`), tự động cập nhật nội dung theo trạng thái tương tác của `PlayerInteractor`:
    * Khi đang cầm vật thể: hiển thị khoảng cách giữ hiện tại (`Scroll: hold distance X.X m`) và gợi ý nhấn chuột phải (`RMB to highlight`).
    * Khi đang ngắm vào vật thể: hiển thị `"Hold LMB to grab\nRMB to highlight"`.
    * Khi không ngắm vào vật nào: ẩn chữ (`""`).
  * **Menu tạm dừng (`Pause Menu`):** Lớp phủ tối toàn màn hình (`RGBA: 0, 0, 0, 0.6`), tiêu đề `"PAUSED"` cùng 2 nút bấm tương tác là `"Resume"` (tiếp tục chơi) và `"Quit"` (`Application.Quit()`).

* **Hiệu ứng hậu kỳ & Lấy nét tự động (`BuildPostProcessing` & `RefreshFocus`):**
  * Tự động bật tính năng hậu kỳ trên camera chính (`UniversalAdditionalCameraData.renderPostProcessing = true`).
  * Khởi tạo `VolumeProfile` toàn cục (`isGlobal = true`) với 2 hiệu ứng:
    * **`Vignette`:** Làm tối nhẹ bốn góc màn hình (`intensity = 0.35f`, `smoothness = 0.45f`) để tăng chiều sâu thị giác.
    * **`DepthOfField` (chế độ `Bokeh`):** Làm mờ hậu cảnh (`aperture = 5.6f`, `focalLength = 50f`). Trong mỗi frame, `RefreshFocus()` đo khoảng cách từ camera tới vật thể `interactor.Target` (hoặc lấy mặc định `4m` nếu không ngắm vật nào) và làm mượt tiêu cự theo công thức mũ (`focusSpeed = 6f`).

---

### 3. Công nghệ và kỹ thuật sử dụng

#### A. Procedural Unity UI (uGUI) Construction
* Khởi tạo toàn bộ cây phân cấp giao diện (`Canvas`, `CanvasScaler`, `GraphicRaycaster`, `RectTransform`, `Image`, `Text`, `Button`) và đăng ký sự kiện `onClick.AddListener` hoàn toàn thông qua mã nguồn C# thông qua các hàm trợ giúp tĩnh (`MakeRect`, `MakeBar`, `MakeText`, `MakeButton`).

#### B. URP Post-Processing Volume API (`UnityEngine.Rendering.Universal`)
* Khởi tạo `VolumeProfile` trong bộ nhớ bằng `ScriptableObject.CreateInstance<VolumeProfile>()` và ghi đè tham số hiệu ứng bằng phương thức `.Override()` của `VolumeParameter`.

#### C. Biên dịch có điều kiện & Nội suy mượt
* Sử dụng `#if ENABLE_INPUT_SYSTEM` để tự động chọn module điều khiển UI tương thích với cấu hình Input của dự án.
* Sử dụng nội suy suy giảm mũ độc lập tốc độ khung hình ($k = 1 - e^{-\text{focusSpeed} \cdot \Delta t}$) để chuyển đổi khoảng cách lấy nét (`focusDistance`) tự nhiên như ống kính máy ảnh thật.
</details>