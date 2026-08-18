namespace FranklinGame.Menu
{
    public readonly struct FranklinMissionDefinition
    {
        public FranklinMissionDefinition(
            string id,
            string title,
            string sender,
            string description,
            string thumbnailAssetPath)
        {
            this.Id = id;
            this.Title = title;
            this.Sender = sender;
            this.Description = description;
            this.ThumbnailAssetPath = thumbnailAssetPath;
        }

        public string Id { get; }
        public string Title { get; }
        public string Sender { get; }
        public string Description { get; }
        public string ThumbnailAssetPath { get; }
    }

    /// <summary>Single source of truth for the sequential sandbox mission entries.</summary>
    public static class FranklinMissionCatalog
    {
        private static readonly FranklinMissionDefinition[] Definitions =
        {
            new(
                "strawberry-pickup",
                "CUỘC HẸN STRAWBERRY",
                "LAMAR",
                "Đến Strawberry và tìm một chiếc xe đủ nhanh cho chuyến lấy hàng. Giữ phương tiện nguyên vẹn khi tới điểm hẹn.",
                "Assets/FranklinMenuScene/Art/Missions/mission-01-strawberry-pickup.png"
            ),
            new(
                "warehouse-recon",
                "TRINH SÁT KHO HÀNG",
                "LESTER",
                "Tiếp cận nhà kho được đánh dấu, quan sát kín đáo và chụp ảnh chiếc xe van giao hàng mà không gây chú ý.",
                "Assets/FranklinMenuScene/Art/Missions/mission-02-warehouse-recon.png"
            ),
            new(
                "downtown-vip",
                "VIP DOWNTOWN",
                "DOWNTOWN CAB",
                "Một khách VIP đang chờ giữa trung tâm. Đến điểm đón trước khi hết thời gian và hoàn thành chuyến xe an toàn.",
                "Assets/FranklinMenuScene/Art/Missions/mission-03-downtown-vip.png"
            ),
            new(
                "range-pressure",
                "ÁP LỰC TRƯỜNG BẮN",
                "RANGE CONTROL",
                "Đến trường bắn phía tây và hạ đủ mục tiêu trong ba lượt. Giữ độ chính xác trước khi bộ đếm thời gian kết thúc.",
                "Assets/FranklinMenuScene/Art/Missions/mission-04-range-pressure.png"
            ),
            new(
                "fuel-run",
                "CHUYẾN HÀNG NÓNG",
                "LAMAR",
                "Nhận chiếc mô tô thể thao tại trạm xăng và đưa nó tới garage trước khi hết giờ. Tránh va chạm để giữ nguyên giá trị lô hàng.",
                "Assets/FranklinMenuScene/Art/Missions/mission-05-fuel-run.png"
            ),
            new(
                "carrier-signal",
                "MẬT LỆNH NGOÀI KHƠI",
                "CONTROL",
                "Dùng drone trinh sát boong tàu sân bay, đánh dấu tín hiệu rồi rời khu vực bằng Gyrocopter mà không gây báo động.",
                "Assets/FranklinMenuScene/Art/Missions/mission-06-carrier-signal.png"
            )
        };

        public static int MissionCount => Definitions.Length;

        public static FranklinMissionDefinition Get(int missionIndex)
        {
            if (missionIndex < 0 || missionIndex >= Definitions.Length)
            {
                return Definitions[0];
            }
            return Definitions[missionIndex];
        }

        public static bool TryGetIndex(string missionId, out int missionIndex)
        {
            missionIndex = -1;
            if (string.IsNullOrWhiteSpace(missionId)) return false;

            for (int index = 0; index < Definitions.Length; index++)
            {
                if (string.Equals(
                    Definitions[index].Id,
                    missionId.Trim(),
                    System.StringComparison.Ordinal))
                {
                    missionIndex = index;
                    return true;
                }
            }
            return false;
        }
    }
}
