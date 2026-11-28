# KINEVERSE-PROJECT ( Laptest2.0 )
Kineverse là một project của nhóm em nghiên cứu về bắt chuyển động của tay để di chuyển vật thể trên không gian 3D 

## 1. Công dụng của project

- **Thử nghiệm cơ chế tương tác vật lý trong không gian 3D:** cầm, kéo và đặt vật phẩm; vật không xuyên qua bàn, tường hay sàn.
- **Dựng môi trường thí nghiệm:**
  - *Main* (phòng lab): đèn trần, bàn dài, các vật phẩm có thể nhặt.
  - *Office* (phòng văn phòng): bàn làm việc, laptop, ghế, cửa sổ, cửa ra vào, chậu cây.
- **Trải nghiệm đầy đủ:** âm thanh được tổng hợp bằng code (không cần file audio ngoài), đèn nhấp nháy, giao diện gợi ý thao tác và menu tạm dừng.

---

## 2. Tính năng chính

| Tính năng | Mô tả | Script liên quan |
|---|---|---|
| Di chuyển FPS | WASD đi lại, Shift chạy (chỉ tiến), Ctrl ngồi xuống. Có head bob, FOV tăng khi chạy. | `PlayerController.cs` |
| Nhặt và kéo vật phẩm | Giữ chuột trái để cầm. Vật không có trọng lực, được kéo bằng sphere sweep nên dừng đúng chỗ khi thả. | `PlayerInteractor.cs`, `Pickable.cs` |
| Điều chỉnh khoảng cách cầm | Lăn chuột khi đang cầm vật: 0.8 m đến 6 m. | `PlayerInteractor.cs`, `InputBridge.cs` |
| Làm nổi bật vật thể | Chuột phải để viền cam quanh vật đang ngắm. Chỉ một vật được nổi bật tại một thời điểm. | `HighlightManager.cs`, `InvertedHullOutline.shader` |
| Tay nhân vật | Hai tay theo camera, có độ trễ khi xoay và nhịp thở. Tay không xuyên qua tường. | `HandController.cs`, `HandClippingPrevention.cs` |
| Đèn nhấp nháy | Đèn trần giữa lúc sáng ổn định lúc chập chờn, kèm tiếng điện kêu. | `FlickerLight.cs` |
| Âm thanh tổng hợp | Tiếng bước chân, nhặt, thả, va chạm, đèn điện và âm nền phòng, tạo bằng thuật toán. | `AudioGenerator.cs` |
| Không khí phòng | Âm nền quạt và ù lưới điện 60 Hz, bụi lơ lửng được chiếu sáng bởi đèn trần. | `RoomAmbience.cs` |
| Giao diện (HUD) | Chấm ngắm, dòng gợi ý thao tác, menu tạm dừng, vignette và depth of field. | `HUD.cs` |

---

## 3. Công nghệ và yêu cầu

### 3.1 Công nghệ sử dụng

| Nhóm | Công nghệ | Dùng để làm gì |
|---|---|---|
| Game engine | **Unity 6000.5.6f1** (đúng version trong `ProjectSettings/ProjectVersion.txt`) | Nền tảng chính để dựng scene, chạy game và biên dịch code |
| Ngôn ngữ lập trình | **C#** | Toàn bộ script gameplay, UI và công cụ Editor |
| Render pipeline | **Universal Render Pipeline (URP) 17.5.0** | Ánh sáng, bóng đổ, post-processing (vignette, depth of field) |
| Shader | **ShaderLab + HLSL** (`InvertedHullOutline.shader`) | Viền highlight quanh vật thể theo kỹ thuật Inverted Hull |
| Nhập liệu | **Input System 1.20.0** | Đọc WASD, chuột, Shift, Ctrl, Esc. `InputBridge.cs` bọc lớp này. |
| Giao diện | **uGUI** (`com.unity.ugui` 2.5.0) | Canvas, chấm ngắm, prompt và menu tạm dừng do `HUD.cs` tạo khi chạy |
| Âm thanh | **Tổng hợp âm thanh bằng code** (`AudioClip.Create`, `SetData`) | Tạo tiếng bước chân, nhặt, thả, điện... mà không cần file audio |
| Model 3D | **FBX, GLB, SketchUp (.skp), Blender (.blend)** | Model thô trong `Assets/Model 3d/`. Unity dùng trực tiếp `.fbx`. File `.glb` cần package hỗ trợ glTF (như glTFast) và project hiện chưa cài package đó. `.skp` và `.blend` là file nguồn, cần xuất sang `.fbx` trước khi dùng trong Unity. |
| Công cụ Editor | **Unity Editor Scripting** (`[MenuItem]`, `AssetDatabase`, `PrefabUtility`, `EditorSceneManager`) | Các menu tự động dựng scene, prefab và rig tay |
| Package khác | AI Navigation 2.0.14, Timeline 1.8.12, Visual Scripting 1.9.11, Test Framework 1.7.0 | Có trong `Packages/manifest.json`. Hiện chưa thấy được gọi trong code gameplay. |
| Kiểm soát phiên bản | **Git** và **Git LFS** | Quản lý mã nguồn và các file model lớn |
| IDE | **Visual Studio** (workload Game với C#), Rider hoặc VS Code | Viết và debug code C#. Cấu hình cho Visual Studio nằm trong `.vsconfig`. |

### 3.2 Yêu cầu

- **Unity 6000.5.6f1**.
- Render pipeline **Universal RP 17.5.0**.
- **Input System 1.20.0**.
- Git và Git LFS, để clone và làm việc nhóm.

> **Lưu ý:** Nếu mở project bằng version Unity khác, Unity sẽ thay đổi cấu trúc file. Các thay đổi này không nên commit thẳng lên nhánh `main`.

---

## 4. Clone và chạy project

### 4.1 Cài công cụ cần thiết

| Công cụ | Dùng để | Ghi chú |
|---|---|---|
| **Git** | Clone, pull, tạo nhánh, commit | Tải tại git-scm.com. Kiểm tra bằng `git --version`. |
| **Git LFS** | Tải các file model lớn (`.fbx`, `.blend`, `.glb`...) | Thường đi kèm Git for Windows. Kiểm tra bằng `git lfs version`. |
| **Unity Hub** | Quản lý nhiều phiên bản Unity | Tải tại unity.com. |
| **Unity 6000.5.6f1** | Mở và chạy project | Cài qua Unity Hub, mục **Installs**. Phải đúng version này. |

### 4.2 Clone project về máy

Mở PowerShell tại thư mục muốn lưu project, rồi chạy lần lượt:

```powershell
git lfs install
git clone https://github.com/JinchanO-o/NCKH_UTT.git
cd NCKH_UTT
git lfs pull
```

- `git lfs install` chỉ cần chạy một lần trên máy.
- `git clone` tạo thư mục `NCKH_UTT` chứa toàn bộ project.
- `git lfs pull` tải nội dung thật của các file model lớn.

Nếu bạn cần làm việc trên một nhánh khác `main`:

```powershell
git branch -a
git switch <ten-nhanh>
```

Khi muốn cập nhật code mới nhất của nhánh đang làm:

```powershell
git pull
```

### 4.3 Mở project trong Unity

1. Mở **Unity Hub**, chọn **Projects > Add > Add project from disk**.
2. Chọn thư mục `NCKH_UTT` (thư mục có sẵn `Assets` và `ProjectSettings`), rồi mở bằng **Unity 6000.5.6f1**.
3. Lần mở đầu tiên, Unity tạo thư mục `Library/` để lưu cache. Quá trình này có thể mất vài phút. Thư mục `Library/` không đưa lên git.
4. Trong cửa sổ **Project**, vào `Assets/Scenes/` và nhấn đúp vào `Main`.
5. Nhấn **Play**. Nhấp vào khung game để khóa chuột và bắt đầu điều khiển.
6. Nhấn **Esc** để mở menu tạm dừng (Resume / Quit).

### 4.4 Các lỗi thường gặp khi clone

| Hiện tượng | Nguyên nhân và cách xử lý |
|---|---|
| File `.fbx` chỉ có khoảng 130 byte, Unity báo lỗi không đọc được model | Chưa chạy `git lfs pull`. Chạy lệnh này trong thư mục project. |
| Vật liệu hiện màu hồng (magenta) | Đang dùng Built-in pipeline. Chạy **Tools > Lab > Use URP** một lần. |
| Thiếu một số model `.glb` hoặc `.blend` | Các file này lớn hơn giới hạn 100 MB của GitHub nên đã bị `.gitignore` loại khỏi repo: `destroyed_buildings.glb`, `medieval_pentagon_castle.glb`, `floating_island.glb`, `dummy.blend` (kèm file `.meta`). Hỏi thành viên nhóm để lấy riêng. |
| Unity báo version không khớp | Mở project bằng đúng version **6000.5.6f1**. Version khác sẽ làm đổi cấu trúc file. |

---

## 5. Điều khiển

| Phím / chuột | Chức năng |
|---|---|
| `W` `A` `S` `D` | Di chuyển |
| Chuột | Quay góc nhìn |
| `Shift` (giữ) | Chạy nhanh (chỉ khi đi tới) |
| `Ctrl` (giữ) | Ngồi xuống và đi chậm |
| Giữ chuột trái | Nhặt vật đang ngắm và giữ; thả ra để đặt vật xuống |
| Lăn chuột (khi đang cầm) | Đưa vật ra xa hoặc lại gần |
| Chuột phải | Làm nổi bật vật đang ngắm; nhấn lại để tắt |
| `Esc` | Mở hoặc đóng menu tạm dừng |

Chấm ngắm ở giữa màn hình. Khi ngắm vào vật có thể nhặt, HUD sẽ hiện gợi ý ở phía dưới.

---

## 6. Cấu trúc thư mục

### 6.1 Thư mục gốc

| Thư mục / file | Vai trò |
|---|---|
| `Assets/` | Toàn bộ nội dung game: scene, code, model, vật liệu, shader, cấu hình. Unity đọc và biên dịch từ đây. |
| `Packages/` | `manifest.json` khai báo các package Unity (URP, Input System...). Unity tự tải package khi mở project. |
| `ProjectSettings/` | Cài đặt project: version Unity, Input, layer, physics, danh sách scene build. Không nên chỉnh tay. |
| `README.md` | Hướng dẫn git chi tiết cho nhóm, gồm lệnh và cách xử lý conflict. |
| `HUONG_DAN_SU_DUNG.md` | Tài liệu này: giới thiệu project, cách chạy và cấu trúc thư mục. |
| `CLAUDE_README.md` | Báo cáo rà soát rủi ro và danh sách việc cần sửa trong `Assets/Scripts`. Dùng cho đội phát triển. |
| `.gitignore` | Danh sách file và thư mục không đưa lên git (`Library/`, `Temp/`, `Build/`...). |
| `.gitattributes` | Quy định file nào dùng Git LFS và cách merge file scene, prefab. |
| `.vsconfig` | Danh sách component cần cài cho Visual Studio. |
| `ignore.conf` | Danh sách bỏ qua mẫu của Unity Version Control. Thường không cần sửa. |

### 6.2 Thư mục `Assets/`

| Thư mục / file | Vai trò |
|---|---|
| `Scenes/` | `Main.unity`: phòng lab, scene chính được đưa vào bản build. `Office.unity`: phòng văn phòng. `SampleScene.unity`: scene mẫu, không dùng trong gameplay. |
| `Scripts/` | Code C# chạy trong game. Chia theo chức năng (xem bảng dưới). |
| `Editor/` | Script chỉ chạy trong Unity Editor để dựng scene và rig tay. Không đưa vào bản build. |
| `Model 3d/` | File model thô, kéo vào scene để dùng. Gồm model dự án NCKH trong `nckh assets/` (cầu, mạch điện, đường...), các model `.glb` và `.fbx` có sẵn, và file nguồn `.skp`, `.blend`. |
| `Materials/` | Vật liệu `.mat`. Tên bắt đầu bằng `Office_` dành cho phòng văn phòng, `Item` cho vật phẩm. |
| `Shaders/` | `InvertedHullOutline.shader`: shader viền highlight cho vật thể. |
| `Settings/` | Cấu hình URP cho PC và Mobile (`PC_RPAsset`, `Mobile_RPAsset`...). |
| `TutorialInfo/` | Công cụ Readme có sẵn trong template Unity. Không liên quan gameplay. |
| `InputSystem_Actions.inputactions` | Cấu hình phím của Input System. |
| `DefaultVolumeProfile.asset` | Hiệu ứng post-processing mặc định (vignette, depth of field...). |
| `New Universal Render Pipeline Asset.asset` | Cấu hình URP mặc định của project. |

### 6.3 Thư mục con của `Assets/Scripts/`

| Thư mục | Vai trò |
|---|---|
| `Core/` | `InputBridge.cs`: đọc phím và chuột. |
| `Player/` | Di chuyển, tương tác với vật, điều khiển tay và chống tay xuyên tường. |
| `Items/` | Vật phẩm có thể nhặt (`Pickable`) và quản lý highlight. |
| `Environment/` | Đèn nhấp nháy và âm nền, bụi trong phòng. |
| `Audio/` | Tổng hợp âm thanh bằng code. |
| `UI/` | HUD, crosshair, prompt và menu tạm dừng. |

Ghi chú:
- Thư mục `Assets/Prefabs/` không nằm trong repo. Nó được tạo ra khi chạy **Tools > Lab > Build Main Scene**.
- Mỗi file trong `Assets/` có một file `.meta` đi kèm. Luôn commit cả hai, nếu không Unity sẽ mất liên kết giữa asset và scene.

---

## 7. Mô tả các script chính

| Script | Vai trò |
|---|---|
| `Core/InputBridge.cs` | Đọc phím và chuột. Tự chọn Input System mới hoặc Input Manager cũ. Các script gameplay chỉ gọi qua lớp này. |
| `Player/PlayerController.cs` | Di chuyển FPS, quay camera, chạy, ngồi, head bob và rung camera. |
| `Player/PlayerInteractor.cs` | Raycast từ tâm màn hình, nhặt và kéo vật, điều chỉnh khoảng cách cầm, bật highlight. |
| `Player/HandController.cs` | Điều khiển tay: vươn tới vật đang ngắm, nâng vật đang cầm, hoặc buông nghỉ. |
| `Player/HandClippingPrevention.cs` | Kéo tay lại khi có tường nằm giữa camera và tay. |
| `Items/Pickable.cs` | Thuộc tính của vật có thể nhặt (khối lượng, va chạm, viền). Phát sự kiện Grabbed, Released, Collided. |
| `Items/HighlightManager.cs` | Quản lý vật đang được highlight. Chỉ có một vật tại một thời điểm. |
| `Environment/FlickerLight.cs` | Điều khiển cường độ đèn nhấp nháy ngẫu nhiên. Yêu cầu object có component `Light`. |
| `Environment/RoomAmbience.cs` | Phát âm nền phòng và sinh bụi lơ lửng. |
| `Audio/AudioGenerator.cs` | Tạo các `AudioClip` bằng thuật toán tổng hợp âm thanh. Clip được cache, chỉ tạo một lần. |
| `UI/HUD.cs` | Tạo Canvas, chấm ngắm, prompt, menu tạm dừng và post-processing ngay lúc chạy. |
| `Editor/LabSceneBuilder.cs` | Dựng toàn bộ scene Main, prefab Item và Player, layer và vật liệu. |
| `Editor/RoomBuilder.cs` | Dựng phòng văn phòng 3D: tường, cửa, cửa sổ, bàn, ghế, cây, đèn và camera. |
| `Editor/PlayerSetup.cs` | Cửa sổ Editor để tạo rig tay dưới camera. |

---

## 8. Công cụ Editor (menu trên thanh công cụ Unity)

| Menu | Khi nào dùng | Lưu ý |
|---|---|---|
| **Tools > Lab > Use URP** | Chạy một lần nếu vật liệu hiện màu hồng (magenta). | Tạo URP asset trong `Assets/Settings` và đặt làm pipeline mặc định. |
| **Tools > Lab > Build Main Scene** | Dựng lại scene Main và các prefab Item, Player. | **Ghi đè** `Main.unity` và prefab hiện có. Chỉ chạy khi chắc chắn. |
| **Tools > Build > Office Room 3D** | Dựng phòng văn phòng trong scene đang mở. | Tạo đối tượng gốc `OfficeRoom_3D` kèm hai camera: nhìn từ trên xuống và nhìn ngang tầm mắt. Chạy lại sẽ xóa `OfficeRoom_3D` cũ và dựng lại. |
| **Tools > Player > Setup Hands** | Tạo rig tay cho camera. | Chọn camera và hai prefab tay, rồi bấm **Build Hand Rig**. Chạy lại sẽ xóa và tạo lại rig. |

---

## 9. Làm việc nhóm với Git (tóm tắt)

- **Không commit thẳng lên `main`.** Tạo nhánh riêng cho công việc của bạn, rồi merge sau khi kiểm tra.
- Trước khi bắt đầu, lấy code mới nhất: `git pull origin main`.
- Các lệnh git chi tiết và cách xử lý conflict với file `.fbx` được ghi trong [README.md](README.md).

---

## 10. Hạn chế hiện tại

- **Tay nhân vật là khối hộp tạm** (Palm và Forearm), chưa có model thật.
- **Vật phẩm không có trọng lực.** Khi thả, vật đứng yên tại chỗ thay vì rơi xuống.
- **Chưa có nhảy.** Trong code không có input Jump.
- **Office.unity chưa nằm trong Build Settings.** Chỉ Main.unity được đưa vào bản build.
- Các tính năng ghi là *(cần test)* trong tài liệu nội bộ chưa được kiểm chứng trên Editor.
