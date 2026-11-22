# Pomodoro

Ứng dụng đồng hồ Pomodoro cho Windows, viết bằng **C# / WPF / .NET 10**. Giao diện tiếng Việt với ba chế độ tập trung, nghỉ ngắn và nghỉ dài.

![Giao diện Pomodoro](docs/screenshots/main.png)

## Tính năng

- Đồng hồ đếm ngược với vòng tiến trình, nút bắt đầu, tạm dừng, tiếp tục và đặt lại phiên.
- Thanh chọn chế độ, thông tin phiên tiếp theo và tiến trình chu kỳ 4 phiên.
- Thời lượng mặc định **25 / 5 / 15 phút**, tùy chỉnh từ **1 đến 180 phút**.
- Chuông MP3 khi hết giờ; chuyển phiên khi chuông kết thúc hoặc khi bấm nút.
- Lưu thời lượng và lựa chọn nhạc chuông qua các lần mở ứng dụng.
- Bố cục co giãn, cài đặt có cuộn, điều khiển bằng bàn phím.
- Giao diện **Sáng / Tối / Hệ thống**, xem trước tức thì và lưu lựa chọn.

## Yêu cầu

- **Windows**, vì ứng dụng sử dụng WPF.
- **.NET 10 SDK** để chạy từ mã nguồn; kiểm tra bằng `dotnet --version`.
- Nếu dùng Visual Studio: phiên bản hỗ trợ .NET 10 và workload **.NET desktop development**.

## Chạy dự án

Mở terminal tại thư mục chứa `Pomodoro.slnx`:

```powershell
dotnet restore .\Pomodoro\Pomodoro.csproj
dotnet run --project .\Pomodoro\Pomodoro.csproj
```

Hoặc mở solution bằng Visual Studio, chọn `Pomodoro` làm startup project và nhấn **F5**.

Biên dịch Release:

```powershell
dotnet build .\Pomodoro\Pomodoro.csproj -c Release
```

Kết quả nằm trong `Pomodoro/bin/Release/net10.0-windows/`. Khi chuyển bản build sang máy khác, giữ nguyên các tệp đi kèm và thư mục `Assets`; máy đích cần **.NET 10 Desktop Runtime**.

## Sử dụng

1. Chọn **Tập trung**, **Nghỉ ngắn** hoặc **Nghỉ dài**, rồi bấm **Bắt đầu**.
2. Bấm **Tạm dừng / Tiếp tục** để điều khiển bộ đếm. Nút **↺** đưa phiên hiện tại về thời lượng ban đầu và chờ bạn bắt đầu lại.
3. Mở **Cài đặt**, nhập thời lượng, chọn chuông nếu cần, rồi bấm **Lưu cài đặt**. Nút **Mặc định** chỉ điền lại các giá trị trong cửa sổ; **Hủy** không thay đổi cấu hình đang dùng.
4. Khi hết giờ, bấm **Sang phiên tiếp theo** hoặc đợi chuông phát xong. Phiên tiếp theo trong chu kỳ bắt đầu tự động.

Đổi sáng/tối trong **Cài đặt → Giao diện**. Chọn **Sáng**, **Tối** hoặc **Hệ thống** để theo giao diện ứng dụng của Windows, rồi bấm **Lưu cài đặt**. Thay đổi được xem trước ngay; **Hủy** khôi phục giao diện đã lưu. Đổi giao diện không đặt lại bộ đếm.

![Giao diện tối](docs/screenshots/main-dark.png)

Chu kỳ:

```text
Tập trung → Nghỉ ngắn → Tập trung → Nghỉ ngắn
         → Tập trung → Nghỉ ngắn → Tập trung → Nghỉ dài → lặp lại
```

Nghỉ dài sau **4 phiên tập trung hoàn thành**. Chọn chế độ bằng tay hoặc đặt lại phiên không xóa số phiên đã hoàn thành trong chu kỳ. Tiến trình chu kỳ được giữ trong lần chạy hiện tại.

| Phím tắt | Thao tác |
| --- | --- |
| Space | Bắt đầu / tạm dừng khi vùng đồng hồ được chọn; trên nút, kích hoạt nút đang có focus |
| Ctrl + R | Đặt lại phiên |
| Ctrl + , | Mở cài đặt |
| F11 / Alt + Enter | Bật hoặc tắt toàn màn hình; khôi phục vị trí, kích thước và trạng thái cửa sổ trước đó khi thoát |
| Tab / Shift + Tab | Di chuyển giữa các điều khiển |
| Esc trong cài đặt | Hủy và đóng |

## Cài đặt và hành vi bộ đếm

- Cấu hình được lưu ở `%LOCALAPPDATA%/Pomodoro/settings.json`. Tệp cấu hình thiếu hoặc không đọc được không ngăn ứng dụng khởi động.
- Nhạc chuông tùy chọn được tham chiếu tại vị trí bạn chọn. Nếu tệp không còn tồn tại lúc mở ứng dụng, chuông mặc định sẽ được dùng.
- Mở cài đặt tạm dừng bộ đếm và chặn tự chuyển phiên; đóng cài đặt khôi phục trạng thái trước đó.
- Nếu chỉ đổi chuông hoặc thời lượng của chế độ khác, phiên hiện tại giữ nguyên thời gian còn lại. Đổi thời lượng của chế độ đang mở sẽ đặt lại bộ đếm và giữ trạng thái chạy / tạm dừng.
- Nếu chuông bị thiếu hoặc trình phát báo lỗi lúc hết giờ, ứng dụng chuyển sang phiên tiếp theo. Chuông mặc định là `Assets/Sounds/alarm_sound.mp3`.
- Thời gian còn lại được tính bằng đồng hồ đơn điệu; nhịp cập nhật giao diện không quyết định độ chính xác của bộ đếm.

## Cấu trúc mã nguồn

```text
Pomodoro.slnx
Pomodoro/
├── App.xaml[.cs]                    Khởi động và vòng đời ứng dụng
├── Shell/
│   └── MainWindow.xaml[.cs]          Bố cục và điều phối chu kỳ
├── Features/
│   ├── Timer/
│   │   ├── Controls/TimerDisplay.*  Toàn bộ cụm đồng hồ dùng chung
│   │   ├── Pages/                   WorkPage, RestPage, LongRestPage, TimeUpPage
│   │   ├── CountdownSession.cs      Tính thời gian bằng đồng hồ đơn điệu
│   │   ├── TimerPage.cs             Vòng đời bộ đếm dùng chung
│   │   └── ITimerControl.cs         Giao tiếp điều khiển bộ đếm
│   ├── Background/
│   │   ├── BackgroundController.cs  Vòng đời ảnh/video nền
│   │   ├── WallpaperBackdrop.cs     Lấy đúng vùng nền đã render để blur
│   │   └── BackgroundOptions.cs     Loại nền và thông số hiển thị
│   ├── Todo/
│   │   ├── TodoListView.xaml[.cs]   Danh sách, chi tiết và kéo thả task
│   │   ├── TodoListView.Motion.cs   Animation thêm task và dịch chuyển bố cục
│   │   ├── TodoListView.Drag.cs     Bản kéo nổi, khoảng trống và tự cuộn
│   │   ├── TodoItem.cs              Dữ liệu công việc và thông báo thay đổi
│   │   └── TodoStore.cs             Lưu danh sách JSON
│   └── Settings/
│       ├── SettingsWindow.xaml[.cs] Cửa sổ cài đặt và xem trước
│       ├── AppSetting.cs            Đọc, kiểm tra và lưu cấu hình
│       └── SettingsData.cs          Dữ liệu JSON
├── Themes/
│   ├── Theme.xaml                  Style giao diện dùng chung
│   ├── ThemeManager.cs             Palette sáng/tối và theo dõi Windows
│   ├── ThemeResources.cs           Nạp resource dictionary
│   ├── WallpaperText.cs            Tắt bóng chữ trên các bề mặt điều khiển đặc
│   └── AppTheme.cs                 Lựa chọn theme
├── Services/Alarm.cs                Phát chuông MP3
├── Assets/Images/                   Ảnh đóng gói
├── Assets/Sounds/                   Âm thanh đóng gói
└── Properties/                      Assembly metadata và publish profiles
tests/Pomodoro.RegressionTests/
tests/Pomodoro.BackgroundTests/
tests/Pomodoro.TodoTests/
docs/screenshots/
```

Namespace đi theo thư mục, ví dụ `Pomodoro.Features.Timer.Controls`. File XAML và code-behind đặt cạnh nhau; tên page/window dùng PascalCase. `MainWindow` điều phối chuyển phiên; các page dùng chung `TimerDisplay` và `TimerPage`. Các thiết lập người dùng vẫn lưu tại vị trí LocalAppData cũ, giữ nguyên định dạng JSON và đường dẫn media tùy chỉnh.

Sau khi tái cấu trúc: build Release đạt 0 lỗi/cảnh báo; 18/18 kiểm thử hồi quy và 12/12 nhóm kiểm thử hình nền đạt. Kiểm thử bổ sung xác nhận các XAML, resource theme, ảnh đóng gói và chuông mặc định được tìm thấy tại đường dẫn mới. Các assertion về opacity đã cập nhật theo giới hạn người dùng đang dùng (0–80%).

Các ảnh nền cũ trong `Assets/Images/` được giữ lại trong dự án. Mặc định giao diện dùng bảng màu sáng/tối; người dùng có thể chọn hình nền riêng trong Cài đặt.

## Danh sách công việc

- Bảng **Việc làm hôm nay** rộng 360 DIP, nằm nổi ở mép phải và đè lên grid giữa. Ẩn/hiện bảng không làm co giãn đồng hồ. Giữ bo góc trái, blur radius 16 và lớp phủ theo theme; dùng chung nguồn ảnh/video hiện có. Các lớp blur lấy đúng vùng từ viewport nền đã render bằng `VisualBrush`, cập nhật tọa độ sau layout và lấy thêm pixel xung quanh mép để tránh đường nối. Không còn đặt các bản Image toàn màn hình bằng Canvas.Left/Top.
- Nhập tên vào thanh **Thêm việc cần làm**, nhấn **Enter** hoặc **↵** để thêm. Task mới hiện nhẹ và trượt vào vị trí trong 180 ms. Checkbox tròn đánh dấu hoàn thành, **×** xóa task. **Thêm chi tiết / Chi tiết** mở ghi chú nhiều dòng với giao diện gọn, không dùng nút Expander mặc định. Ghi chú tự lưu và được ghi ngay khi đóng ứng dụng.
- Danh sách cuộn bằng con lăn/touchpad, thanh cuộn ẩn. Kéo tay nắm **⠿**: bản xem trước đi theo chuột, các task xung quanh trượt nhường khoảng trống; thả xuống để xác nhận và lưu. Thả ra ngoài hoặc nhấn **Esc** để hủy; kéo sát mép để tự cuộn. **Alt+↑/↓** sắp xếp bằng bàn phím. Chỉ cập nhật thứ tự dữ liệu khi thả, không ghi tệp liên tục lúc kéo.
- Công việc hoàn thành nằm trong nhóm **Đã hoàn thành (n)** mặc định thu gọn. Có thể mở nhóm để xem, sắp xếp hoặc đánh dấu lại. Animation dùng API WPF sẵn có, tự tắt các chuyển tiếp khi Windows giảm chuyển động hoặc bật High Contrast.
- Tên, chi tiết, trạng thái và thứ tự được lưu ở `%LOCALAPPDATA%/Pomodoro/todos.json`, riêng với cài đặt bộ đếm. Lỗi lưu có thông báo và nút thử lại; nội dung vẫn giữ trong bộ nhớ.
- Nút **Công việc** ở thanh bên và **×** ở đầu bảng dùng để ẩn/hiện. Cửa sổ hẹp mặc định thu gọn bảng, khi mở vẫn dùng dạng nổi. Chuyển chế độ không tạo lại danh sách; Space và Ctrl+R trong bảng không điều khiển bộ đếm.

Kiểm thử riêng bằng dữ liệu trong artifacts, không ghi vào danh sách cá nhân:

```powershell
dotnet run --project tests/Pomodoro.TodoTests/Pomodoro.TodoTests.csproj --artifacts-path artifacts/todo-test-build
```

Đã kiểm tra thêm/xóa/hoàn thành, lưu ghi chú, thứ tự chèn trước/sau, dữ liệu cũ/lỗi, lưu thất bại, nhập Enter/Space, cuộn ẩn, bảng nổi rộng/hẹp, nguồn nền dùng chung và render theme sáng/tối. Kiểm thử animation trên cửa sổ WPF đã dựng xác nhận task mới có fade/slide, hàng xóm có animation nhường chỗ, thứ tự không đổi trước khi thả, xác nhận lưu đúng và hủy kéo không ghi tệp hoặc để task vô hình. Nhánh giảm chuyển động được kiểm thử qua tùy chọn mô phỏng. Chưa đo độ mượt/FPS khi kéo bằng chuột thật trên desktop.

## Hình nền tùy chỉnh

Khi có ảnh/video nền, chữ dùng bóng tối nhỏ trong theme tối và halo trắng nhẹ trong theme sáng; số đồng hồ có hiệu ứng rõ hơn. Chữ phụ cũng tăng tương phản. Hiệu ứng áp dụng cho lớp chữ, không làm mờ cả component; nhãn trên nút và chế độ được chọn được loại trừ để giữ màu điều khiển. Trở về nền mặc định sẽ tắt bóng/halo, cửa sổ Cài đặt giữ giao diện chữ bình thường. Các tham số nằm trong `Themes/ThemeManager.cs` (`CreateTextShadow`), style dùng chung trong `Themes/Theme.xaml`.

- Trong **Cài đặt → Hình nền**, chọn **Mặc định**, **Ảnh tĩnh** (JPG/JPEG, PNG) hoặc **Video**. Chọn một loại ảnh/video sẽ mở hộp chọn tệp; nút **Chọn tệp…** cho phép thay tệp cùng loại. Hủy hộp chọn tệp giữ nguyên nền đang xem trước.
- Thay đổi hiển thị trực tiếp ở cửa sổ chính, phủ cả thanh bên và giữ tỉ lệ khi cắt phần dư. **Lưu cài đặt** xác nhận; **Hủy**, Escape hoặc đóng Cài đặt khôi phục nền trước khi mở. Chọn **Mặc định** ở mục Hình nền chỉ đặt lại nền; nút **Mặc định** dưới cùng đặt lại toàn bộ cài đặt đang xem trước.
- **Độ tối hình nền** từ 0–85%; **Độ đậm nền component** từ 0–80%. Độ đậm thấp giúp nhìn rõ video nhưng cần chọn nền phù hợp để chữ dễ đọc. Chỉ brush nền có độ trong suốt; chữ và biểu tượng giữ độ đậm đầy đủ. Các trạng thái hover, nhấn, focus, selected và disabled dùng màu riêng, không làm mờ cả nút. Thanh bên và thanh Nhịp tập trung có lớp blur nền riêng, dùng chung nguồn video.
- Loại nền, đường dẫn gốc và thông số hiển thị được lưu trong JSON hiện có, tương thích với cấu hình cũ. Ứng dụng không sao chép, chuyển đổi hoặc nén tệp. Giữ tệp ở vị trí đã chọn; tệp mất, đổi tên, thay đổi hoặc không đọc/phát được sẽ trở về nền mặc định kèm thông báo. Lựa chọn lỗi không tự ghi đè cấu hình đã lưu; có thể Lưu nền mặc định hoặc chọn tệp mới.
- Video dùng duy nhất một `System.Windows.Media.MediaPlayer` ở cửa sổ chính, đọc dần từ URI tệp. Chuyển phiên, đổi theme hoặc chỉnh độ tối/đậm không tạo lại trình phát hay đặt lại vị trí video. Video tự phát, lặp lại và luôn `IsMuted=true`, `Volume=0`; không dùng chung bộ phát chuông.
- Thu nhỏ cửa sổ tạm dừng video; khôi phục tiếp tục tại vị trí cũ. Khi Windows tắt **Animation effects** (`SystemParameters.ClientAreaAnimation`) hoặc bật High Contrast, video dừng ở một khung hình. Thay đổi tùy chọn hệ thống được theo dõi khi ứng dụng đang chạy.
- Không giới hạn thời lượng. Gợi ý clip 10–60 giây, 720p/1080p, 30fps. Thời gian chờ mở tệp là 15 giây, độc lập với thời lượng video. Đổi/bỏ nền hoặc đóng cửa sổ gọi `MediaPlayer.Close()`, bỏ ảnh và hủy theo dõi tệp để giải phóng decoder và khóa tệp; ảnh được giải mã rồi đóng stream ngay.
- Ưu tiên MP4 với H.264. Phần mở rộng MP4 không bảo đảm phát được: hỗ trợ thực tế phụ thuộc codec và thành phần media trên Windows. Các tệp WMV/AVI/MOV/M4V/MKV/WebM cũng được phép chọn để thử; ứng dụng kiểm tra `MediaOpened`, nội dung video và `MediaFailed`, không cam kết hỗ trợ tất cả codec. Tham khảo [Multimedia Overview của WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview).

## Kiểm tra

Chạy trên Windows với .NET 10 SDK, không cần thư viện kiểm thử bên ngoài:

```powershell
dotnet run --project .\tests\Pomodoro.RegressionTests\Pomodoro.RegressionTests.csproj
```

Bộ kiểm tra bao gồm độ chính xác thời gian, tạm dừng / tiếp tục, vòng đời trang, chuyển phiên, chuông lỗi, chu kỳ 4 phiên, nhập liệu và lưu cấu hình. Kiểm thử lưu cấu hình dùng tệp tạm, không thay đổi cài đặt cá nhân.

Xuất ảnh giao diện WPF để kiểm tra bố cục ở kích thước thường và thu gọn:

```powershell
dotnet run --project .\tests\Pomodoro.RegressionTests\Pomodoro.RegressionTests.csproj -- --render
```

Kiểm thử hình nền (Windows có desktop và bộ giải mã video), dùng tệp cấu hình riêng trong `artifacts/background-tests`, không ghi vào cài đặt cá nhân:

```powershell
New-Item -ItemType Directory -Force artifacts/background-tests | Out-Null
curl.exe -fL https://media.w3.org/2010/05/sintel/trailer.mp4 -o artifacts/background-tests/valid.mp4
dotnet run --project tests/Pomodoro.BackgroundTests/Pomodoro.BackgroundTests.csproj --artifacts-path artifacts/background-build
```

Có thể thay video mẫu bằng một MP4 hợp lệ tại cùng đường dẫn. Bộ kiểm thử tạo ảnh nền trắng/đen có vùng tương phản mạnh, ảnh chụp giao diện và dữ liệu lỗi trong thư mục artifacts; không đưa media mẫu vào bản ứng dụng.

Kết quả xác minh ngày 22/11/2026: Release build không lỗi/cảnh báo; 18/18 kiểm thử hồi quy cũ và 11/11 nhóm kiểm thử hình nền đạt. Đã phát thực tế MP4 mẫu 854×480 dài 52 giây; xác minh vị trí phát tiến lên, lặp ở cuối, dừng/tiếp tục khi thu nhỏ/khôi phục, nhánh giảm chuyển động, luôn tắt tiếng, đổi nền nhiều lần, hủy yêu cầu mở cũ và mở độc quyền tệp sau khi giải phóng. Đã kiểm tra lưu/hủy, lỗi lưu, cấu hình cũ, ảnh JPG/PNG, tệp hỏng/mất và ảnh đang dùng bị xóa/đổi tên/sửa. Bộ đếm giữ nguyên đối tượng phiên và trạng thái khi đổi nền/theme.

Đã xem ảnh render WPF của theme sáng/tối trên nền trắng/đen, ở độ đậm component thấp nhất và kích thước cửa sổ tối thiểu; chữ phụ đạt tương phản ít nhất 4.5:1 trong các tổ hợp này. Đã xem cửa sổ Cài đặt và hộp chọn video trên desktop bằng công cụ điều khiển Windows. Giới hạn kiểm tra: nhánh giảm chuyển động được kiểm thử qua nguồn trạng thái mô phỏng, chưa bật/tắt thiết lập Windows thật; chưa kiểm hết codec, GPU, video 720p/1080p, video nhiều giờ, nghe chuông trực tiếp hoặc toàn bộ luồng thao tác bàn phím trên desktop. Các kiểm thử âm thanh xác minh cấu hình tắt tiếng và bộ phát chuông độc lập.
