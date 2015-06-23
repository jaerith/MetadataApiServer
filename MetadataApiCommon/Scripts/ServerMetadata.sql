USE [TEST]
GO
/****** Object:  Table [dbo].[ServerMetadata]    Script Date: 6/11/2015 6:08:47 PM ******/
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
 CONSTRAINT [PK_ServerMetadata] PRIMARY KEY CLUSTERED 
(
	[Action] ASC,
	[Parameters] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime]) VALUES (N'executeCommand', N'POST', N'command=calculate', NULL, N'post_dll\Calculator.dll', CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime))
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime]) VALUES (N'getFileList', N'GET', N'name={user}', NULL, NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime))
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime]) VALUES (N'getNavigation', N'GET', N'name=directory', N'nav\directory.json', NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime))
INSERT [dbo].[ServerMetadata] ([Action], [Type], [Parameters], [Payload], [ExecutableDLL], [AddDtime], [UpdDtime]) VALUES (N'getNavigation', N'GET', N'name=main', N'nav\main.json', NULL, CAST(0x0000A4B400D2C232 AS DateTime), CAST(0x0000A4B400D2C232 AS DateTime))
