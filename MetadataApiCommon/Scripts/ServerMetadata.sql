USE [TEST]
GO
/****** Object:  Table [dbo].[ServerMetadata]    Script Date: 6/30/2015 3:14:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ServerMetadata](
	[Action] [varchar](256) NOT NULL,
	[Type] [varchar](16) NOT NULL,
	[Parameters] [varchar](512) NOT NULL,
	[Payload] [varchar](2048) NULL,
	[ExecutableDLL] [varchar](2048) NULL,
	[AddDtime] [datetime] NULL,
	[UpdDtime] [datetime] NULL,
	[ExecutableCode] [text] NULL,
 CONSTRAINT [PK_ServerMetadata] PRIMARY KEY CLUSTERED 
(
	[Action] ASC,
	[Parameters] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime], [ExecutableCode]) VALUES (N'executeCommand', N'POST', N'command=calculate', NULL, N'post_dll\Calculator.dll', CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime), NULL)
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime], [ExecutableCode]) VALUES (N'executeDynamic', N'POST', N'command=calculate', NULL, NULL, CAST(0x0000A4C700FADE5E AS DateTime), CAST(0x0000A4C700FADE5E AS DateTime), N'using System;
    using System.Collections.Generic;

	using MetadataApiCommon;

    namespace MetadataApiDynamic
    {
		public class DynamicCalculator : MarshalByRefObject, IRunnable
		{
			public DynamicCalculator() {}

			public List<Dictionary<string, string>> Run(List<Dictionary<string, string>> PostBody)
			{
				List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

				if ((PostBody != null) && (PostBody.Count > 0))
				{
					int nFirstValue  = 0;
					int nSecondValue = 0;

					Dictionary<string, string> oValues = PostBody[0];

					if (oValues.ContainsKey("FirstValue"))
					{
						try
						{
							nFirstValue = Convert.ToInt32(oValues["FirstValue"]);
						}
						catch (Exception ex) { string errorMsg = ex.ToString(); }
					}

					if (oValues.ContainsKey("SecondValue"))
					{
						try
						{
							nSecondValue = Convert.ToInt32(oValues["SecondValue"]);
						}
						catch (Exception ex) { string errorMsg = ex.ToString(); }
					}

					int nResultValue = (nFirstValue * nSecondValue) + (nSecondValue - nFirstValue);

					Dictionary<string, string> oResult = new Dictionary<string, string>();
					oResult["Result"] = Convert.ToString(nResultValue);

					oResultBody.Add(oResult);
				}

				return oResultBody;
			}
		}
    }')
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime], [ExecutableCode]) VALUES (N'getFileList', N'GET', N'name={user}', NULL, NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime), NULL)
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime], [ExecutableCode]) VALUES (N'getNavigation', N'GET', N'name=directory', N'nav\directory.json', NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime), NULL)
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime], [ExecutableCode]) VALUES (N'getNavigation', N'GET', N'name=main', N'nav\main.json', NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime), NULL)
