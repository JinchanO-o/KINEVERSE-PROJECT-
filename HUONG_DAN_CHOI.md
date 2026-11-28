# Hướng dẫn chơi – Kineverse (Laptest 2.0)

Người chơi đứng trong phòng lab ở góc nhìn thứ nhất. Nhiệm vụ là **chọn, di chuyển, xoay và ghép các mảnh vật thể 3D** thành mô hình hoàn chỉnh **Cầu Rồng (A3)**.

---

## 1. Bắt đầu

1. Mở project bằng **Unity 6000.5.6f1**, vào `Assets/Scenes/` và mở scene `Main`.
2. Nhấn **Play**, rồi nhấp vào khung Game để khóa chuột.
3. Nhấn **F1** để bật hoặc tắt bảng phím điều khiển ngay trong game.
4. Nhấn **Esc** để tạm dừng (Resume / Quit).

Chấm ngắm nằm giữa màn hình. Dòng gợi ý ở cuối màn hình luôn cho biết bạn có thể làm gì tiếp với vật đang ngắm hoặc đang chọn.

---

## 2. Phím điều khiển

### Di chuyển

| Phím / chuột | Chức năng |
|---|---|
| `W` `A` `S` `D` | Đi lại |
| `Shift` + `W` | Chạy nhanh (chỉ khi đi tới) |
| `Ctrl` (giữ) | Ngồi xuống, đi chậm |
| Chuột | Quay góc nhìn |

### Chọn và cầm vật

| Phím / chuột | Chức năng |
|---|---|
| **Chuột phải** | Chọn vật đang ngắm (vật được viền sáng) |
| **Chuột phải** lần nữa, hoặc vào chỗ trống | Bỏ chọn |
| **Giữ chuột trái** | Cầm và mang vật đang chọn; thả chuột để đặt xuống |
| **Lăn chuột** (khi đang cầm) | Đưa vật ra xa hoặc lại gần, từ 0.8 m đến 6 m |

> Phải **chọn bằng chuột phải trước**, rồi mới cầm bằng chuột trái được.

### Xoay tự do

| Phím / chuột | Chức năng |
|---|---|
| Giữ `R`, rồi giữ **chuột phải** và rê chuột | Lật, xoay vật theo mọi hướng |

### Công cụ theo trục (X / Y / Z)

Khi chọn vật, một **gizmo 3 mũi tên** hiện trên vật: **X màu đỏ**, **Y màu xanh dương**, **Z màu xanh lá**. Mũi tên của trục đang giữ phím sẽ chuyển sang màu vàng.

| Phím / chuột | Chức năng |
|---|---|
| Giữ `X`, `Y` hoặc `Z` + rê **chuột trái** | Trượt vật dọc theo đúng trục đó |
| Giữ `X`, `Y` hoặc `Z` + rê **chuột phải** | Xoay vật quanh trục đó |

> Trong game, trục **Y là hướng lên** (thẳng đứng). Mô hình được dựng trong Blender, nơi trục hướng lên là Z, nhưng trong Unity hướng lên luôn là **Y**.

### Thay đổi kích thước

| Phím | Chức năng |
|---|---|
| Giữ `S`, nhấn `+` hoặc `−` | Phóng to / thu nhỏ 5% mỗi lần nhấn (giữ để lặp) |
| Giữ `S` + `X`/`Y`/`Z`, nhấn `+` hoặc `−` | Kéo giãn / co theo một trục |

> `S` cũng là phím đi lùi: khi đang chỉnh kích thước, nhân vật tạm thời đứng yên.

### Khác

| Phím | Chức năng |
|---|---|
| `F1` | Hiện / ẩn bảng phím điều khiển |
| `Esc` | Tạm dừng / tiếp tục |
| `F9` | Tách lại bước ghép gần nhất (dùng khi thử nghiệm) |

---

## 3. Mục tiêu: ghép Cầu Rồng

Mô hình hoàn chỉnh được ghép qua **5 bước**. Kết quả của bước trước là một mảnh của bước sau:

```
4 mảnh mặt cầu ──► [1] Mặt cầu ──┐
                                 ├──► [3] Cây cầu ──┐
4 trụ cầu ───────► [2] Hàng trụ ─┘                  ├──► [5] Cầu Rồng (A3)
                                                    │
Đầu + thân + đuôi rồng ──► [4] Con rồng ────────────┘
```

| Bước | Ghép gì | Cách ghép |
|---|---|---|
| **1. Mặt cầu** | 4 mảnh `mat_cau` | Đặt các mảnh nối tiếp nhau theo trục **X** (dọc chiều dài cầu), chỉ các mảnh kề nhau mới khớp. |
| **2. Hàng trụ** | 4 trụ `tru_cau` | Xếp thành hàng theo trục **X**. *(Trong scene hiện tại 4 trụ đã nằm sẵn trong một khối `tru_cau 1`.)* |
| **3. Cây cầu** | Mặt cầu + hàng trụ | Đưa mặt cầu **lên phía trên** hàng trụ rồi hạ xuống theo trục **Y**. Có **nam châm** hút vào. |
| **4. Con rồng** | Đầu `dau_rong`, thân `than_rong`, đuôi `duoi_rong` | Nối theo trục **X**: đầu ở một đầu thân, đuôi ở đầu kia. |
| **5. Cầu Rồng** | Con rồng + cây cầu | Đưa con rồng **lên phía trên** cây cầu rồi hạ xuống theo trục **Y**. Có **nam châm** hút vào. Khi xong sẽ hiện **"Dragon Bridge complete!"**. |

Khi một bước hoàn tất, các mảnh bay lại với nhau, có tiếng "pop" và hiện ra vật mới. Vật mới có thể cầm và ghép tiếp như một mảnh bình thường.

---

## 4. Cách khớp (snap) hoạt động

Hai mảnh chỉ khớp khi chúng **thuộc cùng một bước** và được đưa vào **đúng vị trí, đúng hướng**:

1. **Hành lang trục:** khi mảnh đang cầm đi vào gần đường lắp ráp, một **đường dẫn màu** hiện ra theo trục cần lắp. Màu đường dẫn là màu của trục đó trên gizmo. Đường mờ đi khi góc xoay còn lệch nhiều.
2. **Xuyên qua nhau:** trong hành lang này, đúng cặp mảnh đó được phép chồng lên nhau để trượt vào chỗ; mọi vật khác vẫn chặn như bình thường.
3. **Khớp:** khi đủ gần và góc lệch dưới khoảng **15°**, mảnh tự trượt vào vị trí chính xác, kêu "cạch" và dính thành một nhóm. Nhóm này được chọn sẵn để bạn thấy những gì giờ di chuyển cùng nhau.

Bạn có thể đưa mảnh vào bằng cách **cầm mang đi** (chuột trái) hoặc **trượt theo trục** (`X`/`Y`/`Z` + chuột trái). Cách thứ hai dễ căn chính xác hơn.

### Nam châm (bước 3 và 5)

Cây cầu và Cầu Rồng là các vật rất dài, khó căn bằng tay, nên có thêm nam châm:

- Khi mảnh đang cầm ở **phía trên** và trong khoảng **1/4 chiều dài** của vật lớn hơn tính từ chỗ đúng, nam châm tự kéo mảnh vào vị trí và xoay cho khớp.
- Nam châm sửa được góc lệch tới khoảng **45°**. Lệch nhiều hơn, game sẽ nhắc bạn xoay vật cho khớp trước.
- Muốn thoát nam châm: **giật mạnh vật ra xa**. Nam châm chỉ hút lại khi vật đã ra khỏi vùng hút rồi quay vào.

---

## 5. Gợi ý trong game

| Gợi ý | Ý nghĩa |
|---|---|
| **Bóng mờ (ghost) xanh nhạt** | Mô hình hoàn chỉnh A3 dạng trong suốt, cho thấy kết quả cuối cùng. Sau khi khớp được cặp mảnh đầu tiên của một bước, bóng mờ của bước đó hiện ở đúng chỗ cần ghép. |
| **Đường dẫn màu** | Trục và hướng cần đưa mảnh vào. |
| **"Hold X to lock it in"** | Mọi thứ đã khớp, chỉ còn thiếu việc giữ phím trục (khi bước đó yêu cầu). |
| **"Try … along the Y axis, from above"** | Hai mảnh chạm nhau và có thể ghép: hãy đưa vào theo trục và hướng được gợi ý. |
| **Viền đỏ + "… does not fit …"** | Hai mảnh chạm nhau nhưng không ghép được với nhau. Game gợi ý mảnh đúng. |
| **"Finish the … first"** | Mảnh này chưa ghép được vì bước trung gian chưa xong (ví dụ đưa rồng lên mặt cầu khi chưa có hàng trụ). |
| **"Rotate the … to match …"** | Đã vào vùng nam châm nhưng góc lệch quá lớn: xoay vật cho gần đúng hướng. |

---

## 6. Mẹo chơi

- **Chọn trước, cầm sau:** chuột phải để chọn, chuột trái để cầm.
- **Dùng trục cho bước cuối:** khi mảnh đã gần đúng chỗ, giữ phím trục được gợi ý rồi rê chuột trái để trượt vào cho chính xác.
- **Xoay trước khi đưa vào:** khớp góc trước (R + chuột phải, hoặc phím trục + chuột phải), rồi mới đưa mảnh vào.
- **Lùi ra xa với vật lớn:** cây cầu và con rồng rất dài. Lăn chuột để đưa vật ra xa, hoặc lùi lại để nhìn toàn cảnh.
- **Kích thước:** nếu bạn đã phóng to / thu nhỏ một mảnh, lúc khớp nó sẽ tự trở về đúng tỉ lệ của mảnh kia.
- **Làm lại:** nhấn `F9` để tách bước ghép gần nhất ra thành các mảnh rời. Nhấn tiếp để lùi thêm bước.

---

## 7. Dữ liệu nghiên cứu

Mỗi lần khớp, game ghi một dòng vào file CSV `assembly_log.csv` trong thư mục `Application.persistentDataPath`. Mở nhanh bằng menu **Tools > Assembly > Open Log Folder**. Mỗi dòng gồm:

- bước ghép, mảnh di chuyển và mảnh đích, trục lắp;
- thời gian từ lần cầm đầu tiên, số lần thử;
- sai lệch ngang, sai lệch dọc trục, sai lệch góc lúc khớp;
- có dùng phím trục hay không, và có nhờ nam châm (`assist = magnet`) hay không.

Điền **Participant Id** trên component `AssemblyManager` trước mỗi lượt chơi để phân biệt người tham gia trong cùng một file.

---

## 8. Hạn chế hiện tại

Các lỗi này đã được xác định và đang chờ sửa:

- **Hàng trụ chưa được nhận diện:** 4 trụ đang nằm chung trong khối `tru_cau 1`, nên bước 2 và bước 3 (cây cầu) chưa thể hoàn thành.
- **Con rồng chưa có nam châm:** đầu và đuôi phải căn tay vào thân.
- **Vật đẩy người chơi:** đứng quá sát vật đang cầm hoặc đang được nam châm kéo có thể khiến nhân vật bị đẩy trôi đi. Tạm thời hãy giữ khoảng cách với vật khi thao tác.
