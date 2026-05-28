using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using FMO.Models;

namespace FMO.Trustee;

public class OpenDayJson : JsonBase
{
	/// <summary>
	/// 基金产品信息数据传输对象 (DTO)
	/// </summary>
	public class FundInfoDto
	{
		/// <summary>
		/// 产品代码
		/// </summary>
		[JsonPropertyName("fundCode")]
		public string FundCode { get; set; } = null!;

		/// <summary>
		/// 产品名称
		/// </summary>
		[JsonPropertyName("fundName")]
		public string FundName { get; set; } = null!;

        /// <summary>
        /// 开放日期
        /// 格式：yyyymmdd
        /// </summary>
        [JsonPropertyName("openDate")]
		public string OpenDate { get; set; } = null!;

        /// <summary>
        /// 开放类型
        /// 可选值：
        /// - 固开 (固开申购固开赎回)
        /// - 临开 (临开申购临开赎回)
        /// - 申购固开
        /// - 申购临开
        /// - 赎回固开
        /// - 赎回临开
        /// - 申购固开赎回临开
        /// - 申购临开赎回固开
        /// </summary>
        [JsonPropertyName("openType")]
		public string OpenType { get; set; } = null!;

        /// <summary>
        /// 开放状态
        /// 可选值：申购、赎回、申购/赎回
        /// </summary>
        [JsonPropertyName("ifTempOpen")]
		public string IfTempOpen { get; set; } = null!;

        /// <summary>
        /// 是否固定时点计提报酬日
        /// 可选值：是、否
        /// </summary>
        [JsonPropertyName("ifFixedTime")]
		public string IfFixedTime { get; set; } = null!;

        /// <summary>
        /// 预留字段1
        /// </summary>
        [JsonPropertyName("remark1")]
		public string? Remark1 { get; set; }

		/// <summary>
		/// 预留字段2
		/// </summary>
		[JsonPropertyName("remark2")]
		public string? Remark2 { get; set; }

		public FundOpenDay To()
		{
			return new FundOpenDay
			{
				FundCode = this.FundCode,
				FundName = this.FundName,
				OpenDate = DateOnly.ParseExact(this.OpenDate, "yyyyMMdd"),
				OpenType = this.OpenType,
				IfTempOpen = this.IfTempOpen,
				IfFixedTime = this.IfFixedTime,
				Remark1 = this.Remark1,
				Remark2 = this.Remark2
			};
		}
	}
}
