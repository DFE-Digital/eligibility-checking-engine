# ============================================================
# Check-BulkCheckProgress.ps1
# Checks /bulk-check/{id}/progress for all BulkCheckIDs for a
# given LocalAuthorityID and reports any where complete != total.
#
# OPTION A (clipboard) — recommended:
#   1. Set $localAuthorityId below, update Get-BulkCheckIDs.sql, run it
#   2. Select ALL result rows in the grid, Ctrl+C
#   3. Run this script (leave $readFromClipboard = $true)
#
# OPTION B (manual):
#   1. Set $readFromClipboard = $false
#   2. Paste each SQL result row into $bulkCheckBatches below
#   3. Run this script
# ============================================================

$baseUrl          = $env:CYE_API_BASE_URL
$clientId         = $env:CYE_CLIENT_ID
$clientSecret     = $env:CYE_CLIENT_SECRET
$scope            = "local_authority check application admin bulk_check establishment user engine notification free_school_meals two_year_offer early_year_pupil_premium working_families multi_academy_trust"
$localAuthorityId = 330   # <-- change this to the LA you are investigating

if ([string]::IsNullOrWhiteSpace($baseUrl) -or
    [string]::IsNullOrWhiteSpace($clientId) -or
    [string]::IsNullOrWhiteSpace($clientSecret)) {
    Write-Error "Set CYE_API_BASE_URL, CYE_CLIENT_ID and CYE_CLIENT_SECRET environment variables before running this script."
    exit 1
}

# Set to $true to read batch rows directly from clipboard (copy SQL result rows first).
# Set to $false to use the $bulkCheckBatches array below instead.
$readFromClipboard = $true

# OPTION B only: paste each SQL result row as a separate string here.
$bulkCheckBatches = @(
    "2341657f-1f45-4ec1-a44c-77b6f3524ee8|2026-05-13T11:10:15.616,5d883e25-12d1-4f19-a5d3-b6ae31132c7f|2026-05-13T11:01:48.605,9d718a69-932f-4e8d-a3a0-e4c395d23c7c|2026-05-13T10:10:19.157,321840b0-483f-4198-a678-dc07fa74f5b1|2026-05-13T09:10:14.821,8e9eab09-7880-4452-bea8-288958c1b168|2026-05-13T08:10:15.649,efb0a6b8-6023-40eb-bb23-29d20829f443|2026-05-13T07:10:12.654,b5421082-afe1-4189-aa7e-dd42e102d1ca|2026-05-13T06:10:14.835,fd410a7c-08ad-41f5-a513-f3c09e321bb5|2026-05-13T05:11:55.431,74e393c1-9747-490c-8fe1-f6e8dcd5f7d7|2026-05-13T04:10:19.949,2faec4ff-c972-459c-a4ca-f8f31103a480|2026-05-13T03:10:43.153,0cdbd166-febc-4027-a03d-38453c1330fc|2026-05-13T02:21:38.779,47f977ed-1e1b-4c43-b825-038cee06d773|2026-05-13T02:21:34.181,ffd30319-b1a8-46f3-8e6d-3718ddaa8877|2026-05-13T02:21:29.113,ad119cb0-2a35-4b2f-a1f5-13f201d55675|2026-05-13T02:21:12.843,cf8da0eb-d4e5-444d-9b4d-11ba3db4ec77|2026-05-13T02:21:08.273,4fbd9c72-5019-48b1-b635-4bbc938ebf66|2026-05-13T02:21:02.747,44ea7398-d93f-44d5-b3f8-ff34e3eb14da|2026-05-13T02:20:58.428,d28b93a3-69f1-4dde-887c-e97ef9c205d8|2026-05-13T02:20:48.522,04e1e680-89ce-4737-a57e-82d0a0d1f8b6|2026-05-13T02:20:43.917,3c2e7c86-5ff1-4c9c-a8c8-f08398ee6f7b|2026-05-13T02:20:39.217,60cf9b98-4de2-4775-ac6b-3b9068181991|2026-05-13T02:20:34.252,7ddc30a3-c5e4-404e-b3c0-a016b4ae29d9|2026-05-13T02:20:29.323,a0c9c667-9138-4d99-8a8d-e2c4d409d1a6|2026-05-13T02:20:15.838,e149265a-6c2c-44c8-94d1-b603374983f8|2026-05-13T02:20:11.024,21677473-d129-437b-a7ed-4cfe9ef0c26b|2026-05-13T02:20:06.138,55c48c7a-cf6b-4af7-a456-425038644053|2026-05-13T02:20:01.090,cfe83a53-7e98-45b9-bcd5-893451204c85|2026-05-13T02:19:43.960,4be6b9ef-5753-4951-96f5-c57806055a58|2026-05-13T02:19:16.555,ba2bbc99-3c7e-41b4-821b-778f575e96bd|2026-05-13T02:18:24.546,7efb37fb-c918-47d7-8089-9125392e35c1|2026-05-13T02:17:56.273,b974d4d5-f6c1-413d-b443-df955362e034|2026-05-13T02:17:12.423,04eec346-d649-4830-8fa0-5498c12707f5|2026-05-13T02:16:41.384,a95b6f56-c402-4351-b9f4-5328b72c4997|2026-05-13T02:15:50.306,f088eed9-4072-43e7-8f5b-94aca77fb1ac|2026-05-13T02:15:22.001,b907a42c-6036-45cd-9716-7f1a96b86149|2026-05-13T02:14:54.491,e448cbb5-e4bb-498c-803c-46a0a6822701|2026-05-13T02:14:04.315,c2e33f42-d85f-46a8-a997-99fbfffd6285|2026-05-13T02:13:32.703,dcbe84af-f92d-47a9-b303-5f11dbbb8dc5|2026-05-13T02:12:47.016,c40cdb9e-583d-45f4-9b74-adc6471e64d2|2026-05-13T02:12:20.407,5bafb150-4c9b-400a-96be-aaa7efbb40dd|2026-05-13T02:11:36.691,b4a41172-de96-439e-bee3-c2a8ef54f13c|2026-05-13T02:11:09.658,2e8fb8e0-0bec-491e-8700-985c78d310b5|2026-05-13T02:10:34.022,a1a436ec-b513-4617-a06e-049a990d7fc8|2026-05-13T02:10:28.364,a1bb71d0-7977-4e82-8cfe-e03fe1ac5be0|2026-05-13T02:10:22.760,4b23f177-1ea9-4b62-9624-9952ca96a5ac|2026-05-13T02:10:17.035,864e2da2-21bd-420f-a29a-c2f3829c44f8|2026-05-13T02:10:13.413,cc1e7bd8-18de-40a9-b6c9-689e7f83b37c|2026-05-13T02:10:10.923,76378f17-09fa-4d89-9926-c0a92cd1e740|2026-05-13T02:10:01.434,1a1c32dc-5b8a-4b90-83b1-7753665db53d|2026-05-13T02:09:33.189,a614bc03-088e-4239-915c-804df21928a2|2026-05-13T02:08:42.714,74b2e665-9b90-4764-8a2b-7c360c82dec1|2026-05-13T02:08:16.679,49a47166-ca8d-4d6d-8fd6-421810e4f548|2026-05-13T02:07:33.033,6495702f-0fee-48f2-ad8c-2d69cea49f82|2026-05-13T02:06:44.581,7e379981-be4e-4286-8085-00139fcb0443|2026-05-13T02:00:39.206,452df972-379d-4079-a274-e49fb8a4d530|2026-05-13T01:10:23.649,06933dda-2d2b-4def-ba3a-64e21388d25f|2026-05-13T00:12:00.391,7806bd1f-a947-4c5d-b682-d5ce32ad5837|2026-05-12T23:10:23.208,5101d047-52a1-4c47-81cf-8e67f22d5877|2026-05-12T22:10:11.041,930aca27-42ef-4e69-bcd1-6209f4d5f636|2026-05-12T21:10:13.101,030b99cb-7925-4275-a930-c4dc6b7de60c|2026-05-12T20:10:12.015,7ac623d6-abdc-4876-84b1-65aee465aaeb|2026-05-12T19:10:14.299,3960f695-77e0-45be-b04c-347afa11d668|2026-05-12T18:10:15.814,cb5f6967-fcce-4dd7-88ec-c0a0a6fc5495|2026-05-12T17:10:31.983,c95d7fe9-1121-449b-8b2c-02391e7ccccc|2026-05-12T16:10:11.248,867ae3b4-d63a-40b4-bf65-ff446500ac6f|2026-05-12T15:10:16.430,cb9dd5bd-adb2-4916-99f9-118ff3f86e70|2026-05-12T14:28:38.831,c1a0a499-4892-4bde-ab6e-1f3bcb6d3199|2026-05-12T14:23:59.690,a28be966-c989-471f-92fd-546d27275a4d|2026-05-12T14:19:08.433,629226c5-814b-4a2c-9cfa-7f4667c224cc|2026-05-12T14:10:16.612,9344aeb2-fd50-4bad-8eaf-34309aa3678d|2026-05-12T13:52:32.368,8ec2bfde-8146-4b0f-869e-e238ebd7c7fe|2026-05-12T13:10:22.081,95baee52-d87e-404a-8cdf-d73446dbe2b8|2026-05-12T12:10:36.838,cc172d06-d6b8-497f-be4a-0819b968fd8a|2026-05-12T11:10:26.825,ebaac644-e854-4727-aedf-47b9d57ebdc2|2026-05-12T10:10:21.111,4fd4ed4e-787c-4c3d-a559-ab5a9e1f50b4|2026-05-12T09:10:16.504,3ceda8b6-a007-4b12-99d3-ac8200ceca2b|2026-05-12T08:10:20.738,f1150afc-9226-4d4b-b440-efb98fa30770|2026-05-12T07:10:12.120,e4983320-8ee0-4232-9631-5b92764f67e6|2026-05-12T06:10:14.872,c2b7f4e9-e107-4089-bfd3-5cd82c488b51|2026-05-12T05:10:37.517,28580fe1-8f5f-4ff5-932c-53927da0338b|2026-05-12T04:10:56.424,2ba97380-16d0-45dd-b0c8-be8db54d0cbd|2026-05-12T03:10:27.241,226e7c8c-464f-498d-9e40-e2b24a094594|2026-05-12T02:10:13.371,c93032e8-9284-4b72-8019-d7a882e64df3|2026-05-12T02:00:40.746,6228ad08-f953-462f-aeb0-d545f6fedd2b|2026-05-12T01:10:25.877,16c422d9-128b-42ed-986b-9114ae1764bc|2026-05-12T00:12:14.931,c02ec416-ec82-49bf-b142-b53504add96e|2026-05-11T23:10:15.055,0566905b-32f4-4fa3-aa6b-4b2b7d7c50bf|2026-05-11T22:10:14.034,ce896c2c-9917-4af5-820e-6d5d79762a38|2026-05-11T21:10:11.577,9b8c03aa-2c28-43d9-9d5c-a824729da427|2026-05-11T20:10:14.804,0e2ccbc4-1037-409b-8b61-f5d423705911|2026-05-11T19:10:12.815,0667eedf-c514-402a-aab8-01a82f5f1bca|2026-05-11T18:10:11.700,be0103da-0b59-4c8e-9577-f76f263ee59e|2026-05-11T17:10:34.248,76642a4e-43f2-4a56-bbf6-b4cf80d66845|2026-05-11T16:10:19.891,e554840a-f9e0-4af5-b775-e8e3ee9ae466|2026-05-11T15:10:13.797,217a9874-0fff-4b4b-b7bf-95cabd936afb|2026-05-11T14:47:57.902,7295e984-1b47-4a66-af82-142d1085af7f|2026-05-11T14:10:24.304,db262cea-4fda-4678-b743-2eb54a720dd0|2026-05-11T13:10:15.247,f5c13155-143a-41c1-8883-ce86bcc1525c|2026-05-11T12:10:34.452,84f04e73-8cf1-4d1e-a53d-fde72cebc460|2026-05-11T11:10:12.586,c03d4a54-627d-4e28-9ee6-41e36e0b8102|2026-05-11T10:10:17.823",
    "1dd6c774-3853-4f22-bb3e-4cbd7ad2dccc|2026-05-11T09:10:24.290,5da61e12-f94e-4c23-b80c-48adff03ab75|2026-05-11T08:10:15.472,40efc87c-7896-43ab-96a8-287940f7f8b7|2026-05-11T07:10:12.136,3fc177b9-7aaa-42d4-9e37-09353cb585e8|2026-05-11T06:10:11.529,0aafdfd6-d8b1-4262-a0da-bd517c2a57d7|2026-05-11T05:11:56.186,dddc03ed-8460-447f-977b-f0c238443a16|2026-05-11T04:10:22.220,c99caa57-d0f1-4b63-b545-f82613ebffdb|2026-05-11T03:10:31.688,ca4471fb-0113-41ad-bf2e-b48cbaa0ea4f|2026-05-11T02:10:13.392,3f5f9618-d969-4798-b7eb-1125784cf16e|2026-05-11T02:00:41.308,cba3b225-f7e2-40dd-a634-5bb9c9df6801|2026-05-11T01:10:17.779,66e7adf2-3ad4-47a0-be44-ebee3ec0ded8|2026-05-11T00:11:43.789,3a5ffe12-458f-4d12-a5c4-ce0dede74b64|2026-05-10T23:10:12.036,f042da6e-b121-478c-b07d-757e6cfed8da|2026-05-10T22:10:24.447,5d76c16f-374e-4f8e-bfdc-426b1ce298c9|2026-05-10T21:10:11.320,ba07274e-3f03-4e20-a544-96026fe548ca|2026-05-10T20:10:12.069,49388a7e-b207-49e7-b6b0-cdd20dde2f30|2026-05-10T19:10:25.413,31c624a7-fe8e-4658-aafe-bf143b169fb9|2026-05-10T18:10:14.113,58c900b5-5cae-4bea-bb7b-c1b90a14859c|2026-05-10T17:10:12.642,f2931fcb-33b4-4a00-959a-f5531b323dc9|2026-05-10T16:10:13.002,46b91a23-493d-430f-868f-a27f965461f9|2026-05-10T15:10:14.565,3e9dc41a-ff8f-49f3-bb25-8fa59e82f9a8|2026-05-10T14:10:13.911,e28deb2a-8c81-4fbd-8d52-9b7652468fa8|2026-05-10T13:10:44.728,20b5f87f-a6c1-425f-aebb-8637dafca60f|2026-05-10T12:10:23.411,657197f6-5f9b-4fad-a9ea-cd0adda96842|2026-05-10T11:10:14.357,514fc25c-987d-4344-8ce8-23bb7ad4b582|2026-05-10T10:10:12.109,872c8486-eb28-4860-9c55-e74fec33f02f|2026-05-10T09:10:25.498,4c3fde19-4f58-4d83-ad52-25a2aafe5cb6|2026-05-10T08:10:48.259,e1cb2c1c-275f-4b1b-a27a-395809ec9590|2026-05-10T07:10:12.069,8ca08104-6692-47c5-be0b-d2b702d130ad|2026-05-10T06:10:19.909,9e72050d-186a-486a-a3ec-c7cff91d7be6|2026-05-10T05:12:05.169,bb3664e6-8b90-4160-bf05-278cb58d252e|2026-05-10T04:10:22.079,d2cf44f6-11b3-44a4-bad3-5bd2bfecbfbe|2026-05-10T03:10:22.557,6dbb8687-1f75-47f1-9c46-ee3d066fabe5|2026-05-10T02:10:13.522,08672f0a-b8ce-4088-a2f7-58993111831a|2026-05-10T02:00:37.724,c20ffc51-c95b-4190-bc1c-8995a4a46751|2026-05-10T01:10:26.664,b59cc696-5619-4c88-870e-1857582db91d|2026-05-10T00:11:59.307,e6ad55ae-68a0-49f4-83ba-c3d8b0e181a5|2026-05-09T23:10:17.169,73c05351-c1a0-4149-a5fe-5e91f2b39f02|2026-05-09T22:10:13.260,81ec2e1a-5678-4307-a177-e0544a8a21bb|2026-05-09T21:10:15.293,9f1475f9-4945-456a-a560-4c098ac09ba6|2026-05-09T20:10:32.853,5f3f7a67-f50c-4e9f-9624-aeb5bbc23cfb|2026-05-09T19:10:28.759,281bce81-aed3-4e01-9a38-5dfbd1a365b2|2026-05-09T18:10:21.893,893dcad9-a046-41c2-944c-138da83a298d|2026-05-09T17:10:15.538,782d4c5d-ba40-4a42-b360-69b1d6065cb5|2026-05-09T16:10:12.891,2092bc3d-c8bd-493a-81d5-ea0cc663fd02|2026-05-09T15:10:14.405,93739728-686f-4877-a591-02b831cca270|2026-05-09T14:10:11.607,2a467595-7561-4211-b58b-cb24504975f9|2026-05-09T13:10:15.501,72c50f7b-b7b4-4675-b4c2-a7f4dfee86c4|2026-05-09T12:10:23.331,92de5475-4f79-4366-8a08-3f124e1cdadd|2026-05-09T11:10:13.085,1a9de2ca-de1e-4db8-91e2-5ba2a5092215|2026-05-09T10:10:17.771,687303bb-ad37-4d35-93d7-71d460cc341c|2026-05-09T09:10:33.236,df52e31a-fc26-40d3-aa1c-6a90280c95a6|2026-05-09T08:10:27.515,48bdc5bf-852c-4229-a491-7f03eb8c3a53|2026-05-09T07:10:12.062,2d2b49f4-6af8-4410-808e-33b37b464ff4|2026-05-09T06:10:11.458,232c0fbe-d0f4-4e97-940b-04835ae2f822|2026-05-09T05:11:54.585,584bf49f-6c14-4a9d-96a3-50067b5d2131|2026-05-09T04:10:30.912,fe5ad32c-e625-487e-83f3-5896468f9b4a|2026-05-09T03:10:34.295,94d269bb-22ad-475c-a2d8-c3963cbe07eb|2026-05-09T02:10:12.088,78e78469-59b9-45b2-a17e-dcacc14d4fa1|2026-05-09T02:00:43.862,86f52b3e-8d91-4074-8078-1a070a6d21c7|2026-05-09T01:10:32.488,41a71cb1-425a-4ffe-90f3-04061e65e560|2026-05-09T00:12:00.166,11c656ef-28e8-4604-83f0-0289f1c16fb5|2026-05-08T23:10:24.475,22bd2ccc-aa9e-49d4-87cf-648ff4d43f1f|2026-05-08T22:10:11.665,89698b49-472c-467f-88bb-48948a556274|2026-05-08T21:10:12.856,d8604a20-df06-4e1a-ab10-21812adfefb2|2026-05-08T20:10:13.908,c3a1570c-ed67-4d32-b7a9-293f099ef24c|2026-05-08T19:10:11.385,c3f7b609-7989-462b-8acc-44b4009bc321|2026-05-08T18:10:12.924,1e94969a-979e-4504-ac5e-238012289702|2026-05-08T17:10:24.919,becb903f-d5c1-4ef0-9500-fa7f7fff462b|2026-05-08T16:10:12.950,1d1d7fc6-9354-47be-acc2-68a985d78269|2026-05-08T15:10:16.390,539bd722-2a26-4878-8aba-dae2409dafbd|2026-05-08T14:10:12.939,559dbde1-2fa1-47d1-a90f-42446a7669f6|2026-05-08T13:10:22.949,4417e479-e95d-4acf-9715-174bfe52dcb4|2026-05-08T12:10:35.598,3a3b12f9-7de3-4785-bfcf-6a8b4853eb40|2026-05-08T11:10:22.742,e4f8d853-a671-4aff-ae52-be804a644cd1|2026-05-08T10:10:14.558,125d5c75-42a6-4238-adf8-a2971ac2c554|2026-05-08T09:10:13.566,c2d6fd67-8d07-48ce-90f3-74c0d707d36c|2026-05-08T08:10:14.547,15a8edbe-3fc7-453a-999d-fb65c8e2a256|2026-05-08T07:10:12.244,71c1349d-b99f-45fe-bba8-933db739c63a|2026-05-08T06:10:16.798,c6f5b079-4e2d-4a26-a5c3-f0981495ac50|2026-05-08T05:11:52.345,f93f6d0a-b2eb-47e1-9068-2a506116b059|2026-05-08T04:10:20.939,c8a47815-db42-4370-8341-2cff42cf0cb2|2026-05-08T03:10:35.270,a7a35812-4a50-49b5-a025-5a8f9c933509|2026-05-08T02:10:12.547,7204d8ab-0892-4e4d-a789-f123a86bb488|2026-05-08T02:00:44.527,1643f051-aa03-485b-93e4-2cda09f3e6c4|2026-05-08T01:10:30.573,13fe6534-d7ca-4762-8f42-75f2556d3972|2026-05-08T00:12:00.333,1efee2ed-8713-4ddb-9166-967364348dc2|2026-05-07T23:10:14.199,135431e2-32ac-4403-a02b-bf8274d30228|2026-05-07T22:10:14.515,09aaf07b-bc68-4d4b-b474-32aded7cba02|2026-05-07T21:10:20.110,21c248eb-e47c-4e21-a821-bd121d639510|2026-05-07T20:10:34.660,4ba6e171-41e1-400e-81f9-61144790d8fd|2026-05-07T19:10:42.096,3d479492-460f-4931-8fad-24c324173554|2026-05-07T18:10:13.199,394b2e8c-b8ab-4fcc-9702-15155500b4a4|2026-05-07T17:10:25.126,705b8174-a0d2-4c4e-bbe7-8bb1d02a93b8|2026-05-07T16:10:31.131,b0fd80d8-45b9-473d-8e88-a6e243c4a190|2026-05-07T15:10:12.391,51bcd6da-0c68-4d2b-b300-d9cfc88f1ae8|2026-05-07T14:10:12.929,183b2181-1d9c-4d59-afd2-ca52c20d4f54|2026-05-07T13:10:24.046,7777da39-c158-4ee8-bd54-f791fdc40d6c|2026-05-07T12:10:26.799,9eacdf14-2ab0-41f9-85cd-61250ec7fa37|2026-05-07T11:10:19.548,67852a42-0b8c-4b5d-935e-1f3966f148b4|2026-05-07T10:10:18.073",
    "31041677-aeec-4a86-8ca2-e418b575d7f3|2026-05-07T09:10:19.516,62109c0a-f6ef-4d6d-8d26-963f9c9abd0a|2026-05-07T08:10:15.440,7e61de7d-3455-4eee-ba39-25adae607b55|2026-05-07T07:10:11.995,43652e80-1502-4bee-b569-56a114dad419|2026-05-07T06:10:11.697,16456008-2377-4181-ba24-60370362f28c|2026-05-07T05:11:44.237,ee5bbed8-f107-495f-9ff4-f1bd15c5182c|2026-05-07T04:10:23.083,beff34cb-5c4e-4764-a97d-e3c8be1b9ddd|2026-05-07T03:10:20.398,347fce7e-fcb4-4e47-99fb-352cae3e7faf|2026-05-07T02:10:10.658,f9e13f50-e731-4fc8-91c2-f0038bd53f85|2026-05-07T02:00:43.563,2fa3044e-793d-4f01-9b5a-f254713c4477|2026-05-07T01:10:32.162,271d8684-5c92-4777-aa36-060ed1771671|2026-05-07T00:12:03.509,7a3ee628-54a8-4e7c-80e7-174affeaa47f|2026-05-06T23:10:15.986,b26258b9-9141-494a-ad4f-e939486a4a16|2026-05-06T22:10:39.908,9602c37e-8c4f-41d2-962f-19ddbf1d42d7|2026-05-06T21:10:11.811,3ec2433f-065d-42c9-9353-90ce508c898c|2026-05-06T20:10:29.386,0d21c3e8-4955-4e1e-b4fe-3259a458a7f7|2026-05-06T19:10:17.627,0efda9ef-3c90-4971-b699-c7eead2e14fe|2026-05-06T18:10:12.546,65bd4839-8b28-442e-a5b9-eff39a8800fd|2026-05-06T17:10:12.564,1c19abb9-a161-4086-88a7-c795f4197f38|2026-05-06T16:10:18.164,d64951c9-f7eb-477c-b6bb-dc6dd9c9dd02|2026-05-06T15:10:11.020,5eb1077f-843f-4640-adb4-ed0cd67c40a0|2026-05-06T14:10:50.300,593619a2-105b-4386-bd9b-9adb86183a97|2026-05-06T13:10:14.277,fbce69ee-8f4c-456a-b78e-f54f0562d500|2026-05-06T12:10:33.092,3b474fd3-225c-44dd-b714-92638b78875a|2026-05-06T11:10:25.246,91701f1d-453a-4a07-8fa4-eb93c07ba281|2026-05-06T10:10:16.262,f53cd0f4-3649-4881-bdc2-ef0986a70f5c|2026-05-06T09:10:18.514,0dfb4d9b-55a6-4557-86ea-27fa745b362a|2026-05-06T08:10:11.972,7e23e662-7a85-43b3-b278-c4e5bee9a615|2026-05-06T07:10:17.214,b612becd-a535-4ce7-8c9b-f2c1030db3bd|2026-05-06T06:10:19.513,32d4b166-c998-47ca-ba2f-3dc18b0fc2c6|2026-05-06T05:11:37.464,5b0d0ebc-03b5-4e1c-9c19-f1d7ee49addc|2026-05-06T04:10:18.228,8b02e22a-5bdd-4840-8900-b2285a4fc7ac|2026-05-06T03:10:22.533,122a730b-c10d-43e5-b0be-5a6c9c3734b3|2026-05-06T02:22:09.198,ae4bb87a-469b-4803-a7bf-6681a74f760c|2026-05-06T02:22:03.038,37b3666e-3cf9-4720-b4cd-3af312a06aac|2026-05-06T02:21:56.074,9e562b37-2655-46d9-8c6d-41f8cdb778ec|2026-05-06T02:21:47.235,7d53d827-9e49-4a1d-9ba0-4b98694e286f|2026-05-06T02:21:38.046,44e77e0f-dd50-47ff-886c-254781ce57ca|2026-05-06T02:21:32.311,2e358d72-b490-496f-b6b1-59aab68d71ba|2026-05-06T02:21:23.761,e2ac5e99-a7b0-4f27-8150-2e5e5e73ab10|2026-05-06T02:21:09.765,4cb557d9-6ae6-4a4e-8747-7119afe884d0|2026-05-06T02:21:02.932,01a69484-b4bb-448c-98a6-455be9a4dc1d|2026-05-06T02:20:56.475,52d80c06-c61a-41c3-b4d1-c11c3392e8c0|2026-05-06T02:20:49.325,bc210f7d-3bab-4a95-8b4f-858d0cec2260|2026-05-06T02:20:43.710,77700827-9dff-44d5-9a01-aa03d91c513f|2026-05-06T02:20:30.278,9feebeae-7b39-4684-bbf3-e97713e15e24|2026-05-06T02:20:25.709,5d98385c-8f1d-44ba-a6e4-13456cc0adc8|2026-05-06T02:20:20.196,f6cb6289-78f9-42ba-a178-da87dd4dac80|2026-05-06T02:20:14.821,e9759da7-20bb-4c2f-aa66-75afb83699cb|2026-05-06T02:20:08.460,3d22c10e-8476-462d-a616-e59804ac88b0|2026-05-06T02:20:00.658,42a1fadc-5e50-467f-ad28-b2d42478d006|2026-05-06T02:19:12.575,7ccd693f-9fce-4b94-9cfe-f5bc187de81b|2026-05-06T02:18:21.168,3dee00dd-33e7-4e51-86f1-421bace7d711|2026-05-06T02:17:30.876,9b8debab-84de-486c-8daa-34265b2d1518|2026-05-06T02:16:46.713,c0ab6525-f85a-42cd-bbb2-950174bb11f9|2026-05-06T02:16:18.080,1afa0f4b-5fce-48ef-9a04-d41980457427|2026-05-06T02:15:49.375,2eacf9fd-46c6-472b-812e-1eff513a203b|2026-05-06T02:15:22.496,c3730786-3a9a-4235-b2ae-97c25199698f|2026-05-06T02:14:41.608,5f362fe8-a79b-432d-b15a-352a47669e9c|2026-05-06T02:14:14.365,36825450-248c-4521-af7a-06c0252a1f9f|2026-05-06T02:13:23.048,a8e12536-41bc-498f-bdc9-5b737973bbb3|2026-05-06T02:12:57.504,b9d88694-7439-4eee-bf42-c13167163d2c|2026-05-06T02:12:11.627,514a2aef-37af-4db0-81e9-863ef2f48c0d|2026-05-06T02:11:43.236,a1fb4baa-2907-4b51-9305-2204810e61b9|2026-05-06T02:10:56.047,5fde2da0-c454-4fe0-9cbe-29379d74a267|2026-05-06T02:10:25.870,85ca3d15-15d4-49ab-bcdc-63dc145a6dcd|2026-05-06T02:10:20.246,6d3f38d1-63c8-4a86-a9b7-a2552939aa75|2026-05-06T02:10:14.895,9a5537f3-9ad5-4f4b-b558-006393a78aed|2026-05-06T02:10:10.556,e12006b2-71de-48fa-99e7-da2d4a9270e7|2026-05-06T02:10:09.671,90f0b368-3c49-44e1-b5df-0554eb92a52e|2026-05-06T02:10:01.513,8bfa3075-d7c3-4ab8-8976-323c807f0a1f|2026-05-06T02:09:17.272,0ff9c9f9-59fa-4729-805b-94cf0edb89fd|2026-05-06T02:08:48.875,75b2f692-bf04-4f6b-ad36-12aca2b728e5|2026-05-06T02:07:58.355,cc4f638c-182d-43a0-bd93-27de474c618c|2026-05-06T02:07:27.844,b8f18d9e-858d-4d13-a1be-110531695f71|2026-05-06T02:06:42.103,84706a00-cc96-4113-918b-67ae22d27efb|2026-05-06T02:00:40.937,1891e291-6734-43bf-b004-0f9bba2cb649|2026-05-06T01:10:21.557,c05386cb-ae6e-4ff1-9a51-4c4b43969cf1|2026-05-06T00:12:16.120,f411100e-095f-4ed3-9c54-5a1df11cf03d|2026-05-05T23:10:34.410,d596e39b-adb7-4389-a9e2-1be8598047c9|2026-05-05T22:10:12.853,958051bc-2798-4da6-bcbf-cfbf801f9ac6|2026-05-05T21:10:19.119,abd5a193-a4df-416f-bf4c-a54ad35d6892|2026-05-05T20:10:18.650,11a1dfa9-f6b2-4173-be46-abe79079df26|2026-05-05T19:10:11.942,ea389413-18af-4fa1-a66c-882ddf5acc1d|2026-05-05T18:10:13.087,5b8ef2b3-74a2-4d0a-9db7-e69b0f2e1d42|2026-05-05T17:10:21.592,73b8cee4-88d0-48b2-86a6-32a2d81089dd|2026-05-05T16:10:27.897,7023cd10-f293-4e43-98cc-403400f65ddf|2026-05-05T15:10:19.025,7e669a8c-22d2-42a2-83bc-37313b10f3d3|2026-05-05T14:10:29.914,1ad3d4c0-1ed6-4a79-9c8c-d5ea88e9fe81|2026-05-05T13:10:18.855,75e0d724-d02a-4ddc-a03d-5bafbaa39cdd|2026-05-05T12:10:31.802,d43bcf95-5fef-4e2d-8fd6-6d98b279943c|2026-05-05T11:10:24.762,23acebf0-d224-46c9-9ea7-4364d0b0016f|2026-05-05T10:10:17.747,d2ee076b-e80d-4cf4-a78b-1b0b053a9ca4|2026-05-05T09:10:21.134,18e679d2-960d-40be-8de6-09c2c430f077|2026-05-05T08:10:15.790,c92f193a-1442-49b7-a32b-714f8d48d8a0|2026-05-05T07:10:12.661,7c38dc11-7a0b-401b-936b-185f301dbff0|2026-05-05T06:10:12.392,96485179-7b39-4bbe-aa7e-5bdb098f934d|2026-05-05T05:11:38.105,f3953cef-ab0d-4fa4-b965-6a3cb623f95b|2026-05-05T05:11:38.069,8b8c3681-5d0b-40b9-9b3f-4e3bee191d05|2026-05-05T03:10:38.590,62a3c9fd-8ab0-45c9-91ed-3e224b927d26|2026-05-05T02:10:10.208",
    "cd1d9be6-3fd3-429e-92ae-8ab3d5ae7a97|2026-05-05T02:00:36.096,60369a1f-2963-4816-887f-3650f5d4ad7d|2026-05-05T01:10:31.370,73ce1781-8b51-44b8-a6b6-9f289b2ebc61|2026-05-05T00:11:44.171,acfc903d-d948-4379-80da-a17a80c5825f|2026-05-04T23:10:11.241,9792a1a0-b484-4cde-b796-bbfa499336ec|2026-05-04T22:10:12.303,c2a835ec-10f7-4a98-b4a2-da7c1618af27|2026-05-04T21:10:10.663,87ab8c05-7a96-42b2-ac73-5ad8c3d515e8|2026-05-04T20:10:11.592,c08b1d8c-5f51-4bf3-a059-70c49317d50d|2026-05-04T19:10:13.222,45a04e28-d732-4dc4-af58-a7299a3a7396|2026-05-04T18:10:12.263,76df6b53-e04f-4d9c-9113-bb43b0826500|2026-05-04T17:10:15.261,1424ce12-6eb6-4881-a43c-1913dc2f6838|2026-05-04T16:10:10.392,c6d82d5d-0d9c-45af-910c-66b934302262|2026-05-04T15:10:13.320,5963af78-6438-4000-a1af-b72aaddcd4a7|2026-05-04T14:10:14.892,5fc8e4ba-71be-40ce-8b01-5d71a9440890|2026-05-04T13:10:13.629,1e78fcd4-ee76-4d84-a589-74f6d09c4dd5|2026-05-04T12:10:51.171,cd3f59d3-3908-46a9-ab28-b228fce9372b|2026-05-04T11:10:27.083,1e49beb1-5300-4ea6-95f0-1772edadc372|2026-05-04T10:10:12.085,d625e9ba-e368-459a-888f-50f837aae048|2026-05-04T09:10:29.706,c85d007d-e853-4093-bd3e-d8f87877fc2d|2026-05-04T08:10:20.181,2f136b19-3a16-4f34-b251-085f5af6375f|2026-05-04T07:10:11.780,8efe0283-5685-4c2f-8f9a-2ec8f512fc3e|2026-05-04T06:10:12.003,ad35b404-e689-429b-bb6c-9489abe63231|2026-05-04T05:11:52.902,68305a64-daf6-415e-905a-b3c4b3087289|2026-05-04T04:10:26.562,2d61ed83-8120-49f5-85e9-658fc9ced406|2026-05-04T03:10:22.865,c29f19aa-e8f8-46a9-a66d-29ad99c9cdcb|2026-05-04T02:10:11.227,4e94bbe9-be24-4ce1-9fed-fd082ef42c7e|2026-05-04T02:00:40.706,d35fcdf6-db04-4116-a116-581f912cd38e|2026-05-04T01:10:31.820,04273f81-b0d8-47c2-9235-9ca317cbe906|2026-05-04T00:11:52.652,426065bd-91dc-46b4-9f12-c3b122f6ab72|2026-05-03T23:10:11.868,6afe5e59-ef82-453a-b2f5-2b04147a3945|2026-05-03T22:10:19.585,8f3398ed-c005-4dc5-b8ea-f6b25b28436b|2026-05-03T21:10:13.641,e39e8fba-2371-4b4b-bc9f-11bfc7c67d9b|2026-05-03T20:10:11.087,c10fe095-47b6-4f56-80cf-48c50429323e|2026-05-03T19:10:21.285,6df74f78-044c-4782-bc21-a83756c0fc1a|2026-05-03T18:10:12.860,0858ddc1-1cab-4b1b-9e94-d571de9f38d5|2026-05-03T17:10:14.189,582d3a18-9ab1-4db2-abf5-1cc74bc1b400|2026-05-03T16:10:11.901,81287c07-4825-408c-9ea6-74d2cabe1c46|2026-05-03T15:10:12.775,f8128589-6ed9-4c07-8150-13b0801af66d|2026-05-03T14:10:11.729,4883e393-e291-4807-953b-6f5fd1b661e4|2026-05-03T13:10:29.098,59add7e4-1638-489a-914f-2fb080629380|2026-05-03T12:10:25.009,70aeef90-8938-4d1d-bee9-460102f2a32b|2026-05-03T11:10:12.321,fdf198e6-7e2e-4890-9bc5-5d07f62d950a|2026-05-03T10:10:12.737,fbea1318-073c-4a2d-9d8d-541f4c75c9d7|2026-05-03T09:10:19.977,8ba7384a-b7ce-458c-9910-d3d4ba78ddba|2026-05-03T08:10:44.704,e0f7df0d-845b-47a8-bd09-ca4846c07100|2026-05-03T07:10:10.938,3403a131-0d98-4fd8-8849-cabd47c104a1|2026-05-03T06:10:10.616,d7ddb8cf-bcdf-4237-8fff-06a41e6acaee|2026-05-03T05:11:53.811,a5207b0c-7e01-4d2d-b4b7-1fc3e1785a03|2026-05-03T04:10:23.202,8622ed2c-ebc6-4f38-859f-88066c8ba5f5|2026-05-03T03:10:13.195,8eda62cd-0a6f-4a72-9a0e-be39c93af899|2026-05-03T02:11:05.158,1a66e4ff-9e76-44c5-9817-c243eb1eb1b6|2026-05-03T02:00:38.718,a742e322-3008-41b7-9341-5a3e7f43ab73|2026-05-03T01:10:31.719,2827ccdb-1040-4513-832c-9e43e99cbf00|2026-05-03T00:12:16.346,b3339af5-cd73-4525-a121-f3bb5622917b|2026-05-02T23:10:15.545,4fc6cd1a-3c00-4ef1-8856-b91431f27768|2026-05-02T22:10:13.006,9ee8df5b-ed12-42f8-b396-e8b11653ddc3|2026-05-02T21:10:17.022,564bacd4-4e4a-4cd5-b762-3db165ce6ad0|2026-05-02T20:10:45.950,48391592-6422-49e0-8307-32fafea439d8|2026-05-02T19:10:11.697,9a69d366-853b-4deb-a7e4-cb8670a050e9|2026-05-02T18:10:21.071,11b64c66-9a6f-4c5d-8b51-78adaf10d568|2026-05-02T17:10:20.128,57c6c0a3-24b9-48c1-8eb1-2ec5e76e81fd|2026-05-02T16:10:12.237,5b0753ac-7059-4c50-ade0-63c49344d342|2026-05-02T15:10:29.252,17d96446-f1d9-46e8-b64e-d899c6bd8ea0|2026-05-02T14:10:11.419,70fd2202-1b08-4722-9d53-9602b8e342ac|2026-05-02T13:10:12.926,4f313554-0610-404c-a864-30f09622ecae|2026-05-02T12:10:11.585,43b89af3-b7d3-4e51-abba-5a65f5d31a95|2026-05-02T11:10:11.386,f2a5e1af-d455-423f-afc9-ceabe2b1cf5c|2026-05-02T10:10:13.635,ba8963ce-cee4-4b85-bf01-2f946a3329d4|2026-05-02T09:10:12.636,8a1792f0-7cab-4afc-bed4-f01c8ab355c7|2026-05-02T08:10:11.644,321aecde-c6dd-4c7a-acda-96633d3bad94|2026-05-02T07:10:12.415,d8c26003-ddf0-402f-85ae-4d7d88a2cdd6|2026-05-02T06:10:12.058,56c31beb-a5af-4315-9e92-c8bd79816316|2026-05-02T05:11:52.518,1732f00e-ecf8-4cf7-8661-01653308e72a|2026-05-02T04:10:16.246,7179f2d5-567a-43a8-a318-a61a504c9d6e|2026-05-02T03:10:33.606,cd12ba91-abae-4a0f-8751-5ba2a06c2298|2026-05-02T02:10:11.300,dda9226b-73ef-4791-81cc-673fd3587928|2026-05-02T02:00:40.449,f692334e-812b-4210-b0a0-2be68c6833d2|2026-05-02T01:10:30.239,9ce1f078-e15e-477e-89dd-467212118a6f|2026-05-02T00:11:58.054,147272e4-a2f7-4110-b79e-c2626426b808|2026-05-01T23:10:21.310,1a522746-8211-44cb-9998-89f1f1451c3f|2026-05-01T22:10:20.814,227d3228-6f50-4eca-a7f6-613e08ffa7b6|2026-05-01T21:10:23.450,61c267a1-cc0c-4365-a0d2-02515f302027|2026-05-01T20:10:12.268,83f5fdce-7deb-458c-83e0-be7c8aef32c5|2026-05-01T19:10:23.621,e7d7ee69-2ab3-4978-b1f8-b531cc0f6bc1|2026-05-01T18:10:11.112,f47e822d-ebe9-401c-a992-b36200e1f73a|2026-05-01T17:10:10.961,bc7c1395-5e33-46a9-92c6-18edb1d37dc3|2026-05-01T16:10:22.077,4cc82398-0db8-48db-a67b-9bb5cb287ba6|2026-05-01T15:10:28.229,db88c040-9f45-40b1-a364-9d3db90e2be8|2026-05-01T14:10:12.230,50f04f05-156b-4026-bf52-0386ca51c59c|2026-05-01T13:10:11.698,9b203d98-ff75-496f-9b1b-f1d2c711246f|2026-05-01T12:10:25.480,a750bff5-f606-4d85-ab58-eb4a45d0205a|2026-05-01T11:10:13.466,7e690016-9402-447e-9585-8be3f78db54c|2026-05-01T10:10:24.279,f4c76a04-80da-4b84-83ca-f2865f0e09e1|2026-05-01T09:10:20.253,42291af0-a332-4864-aa1f-67162ab2da86|2026-05-01T08:10:17.874,a00b6198-8f31-4051-8b0e-b8d12231dbf5|2026-05-01T07:10:12.607,39956110-7460-47ac-9666-2a950d804867|2026-05-01T06:10:11.196,d2f74a12-14b7-4fe8-91a6-02ad0705ad72|2026-05-01T05:11:55.005,1c3bc4b7-3ae7-40ba-8e4b-2e78454e8eb8|2026-05-01T04:10:17.681,10833b38-2931-4ee9-ab8a-b687e68c6654|2026-05-01T03:10:20.267,655db629-d560-4500-8416-ce3c44740969|2026-05-01T02:10:12.678"
    # "paste-batch-2-here"
    # "paste-batch-3-here"
)

# ============================================================

# --- Load batches from clipboard or array ---
if ($readFromClipboard) {
    Write-Host "Reading batch data from clipboard..." -ForegroundColor Cyan
    $clipped = Get-Clipboard
    # Keep only lines that look like batch data (start with a GUID)
    $bulkCheckBatches = $clipped |
        Where-Object { $_ -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-' } |
        ForEach-Object { $_.Trim() }
    if ($bulkCheckBatches.Count -eq 0) {
        Write-Error "Clipboard does not contain valid batch data. Copy the SQL result rows (not the header) and try again."
        exit 1
    }
    Write-Host "Found $($bulkCheckBatches.Count) batch row(s) in clipboard." -ForegroundColor Green
}

# --- Token helper ---
function Invoke-TokenRequest {
    param($Uri, $Body)
    try {
        $resp = Invoke-RestMethod -Uri $Uri -Method POST -ContentType "application/x-www-form-urlencoded" -Body $Body
        if (-not $resp.access_token) { throw "No access_token in response: $($resp | ConvertTo-Json)" }
        return $resp
    }
    catch {
        Write-Error "Failed to obtain token: $_"
        exit 1
    }
}

# --- Get OAuth2 token ---
Write-Host "Requesting access token..." -ForegroundColor Cyan

$tokenBody = @{
    grant_type    = "client_credentials"
    client_id     = $clientId
    client_secret = $clientSecret
    scope         = $scope
}

try {
    $tokenResponse = Invoke-TokenRequest -Uri "$baseUrl/oauth2/token" -Body $tokenBody
}
catch {
    Write-Error "Failed to obtain token: $_"
    exit 1
}

$accessToken     = $tokenResponse.access_token
$tokenExpiresIn  = if ($tokenResponse.expires_in) { [int]$tokenResponse.expires_in } else { 3600 }
$tokenAcquiredAt = [datetime]::UtcNow

if (-not $accessToken) {
    Write-Error "Token response did not contain an access_token. Response: $($tokenResponse | ConvertTo-Json)"
    exit 1
}

$headers = @{ Authorization = "Bearer $accessToken" }
Write-Host "Token obtained (expires in ${tokenExpiresIn}s)." -ForegroundColor Green

# --- Parse input (flatten all batches, latest first from SQL) ---
$entries = ($bulkCheckBatches -join ',') -split ',' | ForEach-Object {
    $parts = $_.Trim() -split '\|', 2
    [PSCustomObject]@{ Id = $parts[0].Trim(); SubmittedDate = $parts[1].Trim() }
} | Where-Object { $_.Id -ne '' }

$incomplete  = [System.Collections.Generic.List[PSCustomObject]]::new()
$errors      = [System.Collections.Generic.List[PSCustomObject]]::new()
$allResults  = [System.Collections.Generic.List[PSCustomObject]]::new()
$total_ids   = $entries.Count

Write-Host "Checking progress for $total_ids bulk check(s) in parallel (ThrottleLimit=5, chunks of 50, 5s pause between chunks)..." -ForegroundColor Cyan

# Split into chunks of 50 so we can refresh the token and pause between chunks
$chunkSize  = 50
$chunks     = for ($idx = 0; $idx -lt $total_ids; $idx += $chunkSize) {
    , ($entries[$idx .. ([math]::Min($idx + $chunkSize - 1, $total_ids - 1))])
}
$processed  = 0

foreach ($chunk in $chunks) {
    # Refresh token if it will expire within 90 seconds
    $elapsed = ([datetime]::UtcNow - $tokenAcquiredAt).TotalSeconds
    if ($elapsed -ge ($tokenExpiresIn - 90)) {
        Write-Host "Token nearing expiry, refreshing..." -ForegroundColor Yellow
        $tokenResponse   = Invoke-TokenRequest -Uri "$baseUrl/oauth2/token" -Body $tokenBody
        $accessToken     = $tokenResponse.access_token
        $tokenExpiresIn  = if ($tokenResponse.expires_in) { [int]$tokenResponse.expires_in } else { 3600 }
        $tokenAcquiredAt = [datetime]::UtcNow
        Write-Host "Token refreshed." -ForegroundColor Green
    }

    $bearerHeader = "Bearer $accessToken"
    $apiBase      = $baseUrl

    $chunkResults = $chunk | ForEach-Object -Parallel {
        $entry      = $_
        $authHeader = @{ Authorization = $using:bearerHeader }
        $uri        = "$($using:apiBase)/bulk-check/$($entry.Id)/progress"

        try {
            $response = Invoke-RestMethod -Uri $uri -Method GET -Headers $authHeader
            $total    = $response.data.total
            $complete = $response.data.complete
            [PSCustomObject]@{
                SubmittedDate = $entry.SubmittedDate
                BulkCheckID   = $entry.Id
                Total         = $total
                Complete      = $complete
                IsComplete    = ($complete -eq $total)
                Error         = $null
            }
        }
        catch {
            [PSCustomObject]@{
                SubmittedDate = $entry.SubmittedDate
                BulkCheckID   = $entry.Id
                Total         = $null
                Complete      = $null
                IsComplete    = $false
                Error         = $_.Exception.Message
            }
        }
    } -ThrottleLimit 5

    foreach ($r in $chunkResults) {
        $allResults.Add([PSCustomObject]@{
            SubmittedDate = $r.SubmittedDate
            IsComplete    = $r.IsComplete
        })
        if ($r.Error) {
            $errors.Add([PSCustomObject]@{
                SubmittedDate = $r.SubmittedDate
                BulkCheckID   = $r.BulkCheckID
                Error         = $r.Error
            })
        }
        elseif (-not $r.IsComplete) {
            $incomplete.Add([PSCustomObject]@{
                SubmittedDate = $r.SubmittedDate
                BulkCheckID   = $r.BulkCheckID
                Total         = $r.Total
                Complete      = $r.Complete
                Remaining     = $r.Total - $r.Complete
            })
        }
    }

    $processed += $chunk.Count
    Write-Progress -Activity "Checking bulk checks" -Status "$processed / $total_ids" -PercentComplete ($processed / $total_ids * 100)

    # Pause between chunks to avoid overwhelming the database
    if ($processed -lt $total_ids) {
        Write-Host "  Pausing 5s before next chunk..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 5
    }
}

Write-Progress -Activity "Checking bulk checks" -Completed

# --- Results ---
Write-Host ""
Write-Host "===== RESULTS =====" -ForegroundColor Yellow

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) request(s) failed:" -ForegroundColor Red
    $errors | Format-Table -AutoSize
}

$outputDir  = "C:\Test Projects\Investigation"
$timestamp  = Get-Date -Format "yyyyMMdd-HHmmss"
$null = New-Item -ItemType Directory -Force -Path $outputDir

if ($incomplete.Count -eq 0) {
    Write-Host "All $total_ids bulk check(s) are fully complete." -ForegroundColor Green
}
else {
    Write-Host "$($incomplete.Count) incomplete bulk check(s) (out of $total_ids):" -ForegroundColor Red
    $incomplete | Sort-Object SubmittedDate -Descending | Format-Table -AutoSize

    $csvPath = Join-Path $outputDir "incomplete-bulk-checks-LA${localAuthorityId}_$timestamp.csv"
    $incomplete | Sort-Object SubmittedDate -Descending | Export-Csv -Path $csvPath -NoTypeInformation
    Write-Host "Exported to: $csvPath" -ForegroundColor Cyan
}

if ($errors.Count -gt 0) {
    $errCsvPath = Join-Path $outputDir "bulk-check-errors-LA${localAuthorityId}_$timestamp.csv"
    $errors | Export-Csv -Path $errCsvPath -NoTypeInformation
    Write-Host "Errors exported to: $errCsvPath" -ForegroundColor Cyan
}

# --- Chart: Complete vs Incomplete by hour of submission ---
if ($allResults.Count -gt 0) {
    Write-Host ""
    Write-Host "===== SUBMISSIONS BY HOUR (UTC) =====" -ForegroundColor Yellow
    Write-Host "  Legend: " -NoNewline
    Write-Host "█ Complete " -ForegroundColor Green -NoNewline
    Write-Host "█ Incomplete" -ForegroundColor Red
    Write-Host ""

    $hourData = @{}
    foreach ($r in $allResults) {
        $hour = ([datetime]$r.SubmittedDate).Hour
        if (-not $hourData.ContainsKey($hour)) {
            $hourData[$hour] = @{ Complete = 0; Incomplete = 0 }
        }
        if ($r.IsComplete) { $hourData[$hour].Complete++ }
        else                { $hourData[$hour].Incomplete++ }
    }

    $maxCount = ($hourData.Values | ForEach-Object { $_.Complete + $_.Incomplete } | Measure-Object -Maximum).Maximum
    $barWidth = 50

    foreach ($hour in ($hourData.Keys | Sort-Object)) {
        $c      = $hourData[$hour].Complete
        $inc    = $hourData[$hour].Incomplete
        $cBar   = [math]::Round(($c   / $maxCount) * $barWidth)
        $iBar   = [math]::Round(($inc / $maxCount) * $barWidth)
        $label  = "{0:D2}:xx" -f $hour
        Write-Host -NoNewline ("  {0}  " -f $label)
        Write-Host -NoNewline ("█" * $cBar)  -ForegroundColor Green
        Write-Host -NoNewline ("█" * $iBar)  -ForegroundColor Red
        Write-Host ("  ($c / $($c + $inc))")
    }
    Write-Host ""
    Write-Host "  (count shown as: complete / total per hour)" -ForegroundColor DarkGray

    # --- Export hourly summary to CSV (for Excel) ---
    $hourlySummary = foreach ($hour in ($hourData.Keys | Sort-Object)) {
        $c    = $hourData[$hour].Complete
        $inc  = $hourData[$hour].Incomplete
        $tot  = $c + $inc
        [PSCustomObject]@{
            Hour_UTC          = "{0:D2}:00" -f $hour
            Complete          = $c
            Incomplete        = $inc
            Total             = $tot
            "Complete_%"      = if ($tot -gt 0) { [math]::Round($c   / $tot * 100, 1) } else { 0 }
            "Incomplete_%"    = if ($tot -gt 0) { [math]::Round($inc / $tot * 100, 1) } else { 0 }
        }
    }

    $chartCsvPath = Join-Path $outputDir "hourly-summary-LA${localAuthorityId}_$timestamp.csv"
    $hourlySummary | Export-Csv -Path $chartCsvPath -NoTypeInformation
    Write-Host "Hourly summary exported to: $chartCsvPath" -ForegroundColor Cyan
}
