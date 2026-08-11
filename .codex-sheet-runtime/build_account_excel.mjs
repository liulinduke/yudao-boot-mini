import fs from 'node:fs/promises'
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const desktop = 'C:/Users/10378/Desktop'
const outputPath = `${desktop}/Facebook账号信息_按分隔符拆分.xlsx`
const previewPath = `${desktop}/Facebook账号信息_预览.png`

const rawRows = [
  '61577601265637|29bexmIG|ZV5UCVUGZRRXPYZ77WHSRBAPTF7MNA7U|c_user=61577601265637;xs=23:PL1zJFUGYR6_HQ:2:1750149317:-1:-1;fr=0I04pzeMNonawTEH3.AWcsP-SrsG0GVLB_d9FSd6vkpSmilb6Vhs2U8npomLaA0XHTaj8.BoUSjL..AAA.0.0.BoUSjL.AWex97VH89PbpHtX-*M*-ymEkP8;datr=xChRaPRow_M2GGwDrjzwukXW;|EAAAAUaZA8jlABO9G6WLncXRY5tLZBcKCvuQ2oIVA5Ev6SjcsXgVBddHcI8UgbM2QT9vZCFA7yuZAzhwnLvTyZCZBA1rbEUKcxp1X3h2rEIeKxMZCb8wpsoPcSBjXkCYTOtrGbDlCo1YrsqmP0E6nNoZAdI6s61w0m1oaSxclOZBlvlNnKS30Nrwf40hj03KjVZC0BAY6RKEfC0lgZDZD|laac3o2keigq@mailto.plus',
  '61576868167895|296cKA4OR|AVPI65ENFLK3GGO7LMLBQI5LXBMOMMAH|c_user=61576868167895;xs=27:IQ2nrDYjc79sZQ:2:1754814912:-1:-1;fr=0IvZaMZKkP2TKA6tJ.AWeYFZKmSi6UZtXMoFpUiUiqJGs4ndNsW_X9EVzP6_ww5ADjYFU.BomFm_..AAA.0.0.BomFm_.AWeYU4tcF7D7Q7XMxD4OMy932_0;datr=v1mYaLdAEiI83-Nzgczx9SEy;|EAAAAUaZA8jlABPB8f2A2EiyZCSfu1IT08pmtDzIqFsmd46ip7CckmBmalGAJlzcHfnPZC79nXvbRgZBpicoDpRKr0uylPlaELSVamECC98QCd9fVZCvmVjAIbPrrZBUeBp59jEnNoeP6kG5S3wOx1nhi3xgZC7ThrR8uHKUAws0KicvKhC81ZAjvp0cPFlQZCJLSAC3xcSQZDZD|dxk4hjlw1pvj@mailto.plus',
  '61577794970302|29MROtPBJU|KYQUPK6LPMSXMPTNLW3AI5B7V2HLNMCE|c_user=61577794970302;xs=29:6B1S295p6Xp7Tg:2:1750311654:-1:-1;fr=0JY8D3QFeLFZs1cZ3.AWeck5Hhn_RBOuaJF8g3KlUaDPZmzTg9V473Quuph_qgVvJ-1Ic.BoU6Lr..AAA.0.0.BoU6Lr.AWfqQd_p4-RxYiTFSXBgAnB_ijc;datr=5aJTaEpd9GJs057fkc6ds_2X;|EAAAAUaZA8jlABO0EK4AnbqEwSMZAuuofqu7XyiWOZC85yMNXcEsMSRGpO8anPX7Bq3lDVrZBSOt5PgDfjdBTO0uTwsNPyAfgj6Gay1XcWFOlU3viFWbpLA86e6xMiGs4NiQoflkZAM3IamxL19roIWQcSvZAIhG8AZAvUnG6bx2nWGuNrZCVNhxASVHJWZBGntiQ08XDmdWsL3QZDZD|i3u9rj6nbwf3@mailto.plus',
  '61577406598017|29kXe17x|KYJYHS7WFYEYU56SFUFK3U4UCKSCT5SH|c_user=61577406598017;xs=24:DvrqVgVMmyPuIA:2:1785503186:-1:-1;fr=0ga8nlu6Q7VgfUF2r.AWdUX6tbUyHmEcJOXOkMVK7Lo9RktisqYpbbujK2wD2VDc3jIe0.BqbJ3Z..AAA.0.0.BqbJ3Z.AWe8B-6mmZIEPF3XAcHCNtXDGlQ;|EAAAAUaZA8jlABSKhYxqGWps36ZAuHc09OoVf21LuAMMZBMuZB6msVeIjbGLEmV37tT1ZAZAS84P77gh4aJwq468o2l43gBvjnRt9SOa6QKpMytiCDmNnJE7vaCyoeZB2ukbPkcN06hUMZBE08FelWyQf0J992ZBnXJIIZBert7P19qtOCC3KSd7jY46bf6F5wM7bEfeApeZA4ZC2ZCwZDZD|asugafy@mailto.plus',
  '61577342393159|29Hd0yHm|2MBWX4CK6AA5NPYQAKLETP6TVIMSGEOS|c_user=61577342393159;xs=2:UmXHAdyO4ZgogA:2:1785505095:-1:-1;fr=01ERBCKWuT3XApuEv.AWdZmCZ4ME-SgEmkB7Jtu2LUitVhccsJCsqKuzoUmGTgMhsoSz8.BqbKVE..AAA.0.0.BqbKVE.AWenzHV26S0eiSdzKXYbD-nKAoI;datr=TY5HaLZxURkNdztKCW30Dbl5;|EAAAAUaZA8jlABSGMKrN7a6tdVxajkq2F2xHWyBwxQR3pEldZAE7z8i4aHK4aOl1rbzgPZADSBw1BUoxBDbJX4ba2ZCo4h62x0Ch1zeIWIwsa6JyXpWjcXE3YsRUyzAueQFFSyZBtYEVGqGH9La3ZCicoZBZCuEQLqM8YmBRbJiNKW5EOzv0iSgNPuMAZC3nhldZAzqVnYH6aZCm6h3OZAHFqZAtA7nVZCuhwZDZD|gzu5hyxqnzwv@mailto.plus',
  '61577151008112|29M6ZD7Z|2S3222STOG4W2B5HXDLVB6KZ5ZGBGDV3|c_user=61577151008112;xs=24:4UnM8lGPtEgv6w:2:1785509541:-1:-1;fr=0OhOVlcsWdgeMl3t9.AWcCa_Ub4nZs7aQ8cR_bz24kmcxbjB7pppcxaQnfnS6WFc9G1vs.BqbLas..AAA.0.0.BqbLas.AWcGVXLfWBUFw5vGlWOjI_LVFj8;|EAAAAUaZA8jlABSIURfyMXxTQ60EfIso8haUqY1sh10rmMEZAhDN9qKKlzpDZAXKb4kMAAS9xYrAqDt4hoOVZB9Q87DdDInJyH7e3OChPaxdmjZCaZBXuVZCp3p8BK5wvThusZB7qOZAcAss5lkxbYgH9jXeNbbESyqfLGdrgmkiiDXI5uZBZAfMeV0whwERUHdQUNykhN0cTDBNUQZDZD|ycyneoh@mailto.plus',
  '61576991476300|29hFIchL|N5ZSQC4FX7IQOPP7JNIOCEN5IUSHMI5Z|c_user=61576991476300;xs=47:OVi1EEHmd_7hsQ:2:1785502038:-1:-1;fr=0De7z5OC5kExytF9h.AWfUbvOqH4hmb_TitnVdPN5VEiSuHbSPo2Xh9_BdtWrXkGGFL6w.BqbJlf..AAA.0.0.BqbJlf.AWfDW7fpG_frQeP0SYOj_2Q1HdI;|EAAAAUaZA8jlABSAZAMiUZCcmJbQFw7ysWm84BaUAIWjUApUZC7OMxCtD1iLXkqRytzprO9OwT7moF98B2ZChqtgWJiAZAbpvjjsQT4Or2ZCTyqVF97HasPrkr1nxRsNMZBfpfoZCIagZBrg1P6uoNQpRb6oZAlhDELfcTe8FPWdEYAOZC4Paq3jSluTc5GN0QXp8ZBZAFZAozUflqwZDZD|gamoagauw@mailto.plus',
  '61577703817154|29wUfvkM|U3IOWQBVNOLF4CM225XCNK4QPYGDZWIZ|c_user=61577703817154;xs=46:n-A1UmOwtj5WGA:2:1785504091:-1:-1;fr=0PVYGkR4tZLvoORV8.AWc_HzzsREb8pUk8hKj1IwPWDgbdpR3BDvJsU82i45zqyhlx4cU.BqbKFa..AAA.0.0.BqbKFa.AWdvryKyqOJV93a_l_Sixtd9JtM;datr=m1dfaFgmZk7bSqCvvaV079-z;|EAAAAUaZA8jlABSNS2bZCFmqr60mlB3sQkpZBm3ANO1wLb1pq3PhccKpZB6HZB9Y19O1ZAKWK2uWGK4oT0IAp5QnmaNp3YdqdYyu113pxLqOJ7a7ZAzfiJL0dqVO8NAZAS2ONZAZCJ26zZA6YLk0BSR8rr6dOpoZC4OJa4VLVOFpmGcensQWvKmJONUcm5XIeDu49H15X12NbFIHHuZBbaen50lqpD30FrfwZDZD|n01v3vue5cnw@mailto.plus',
  '61577804631180|29QBUuHdQ|36RE7ZIMKRSYQ6YWNMFMN5Y6HJQARNUF|c_user=61577804631180;xs=42:PTAaYjzZLiZCrg:2:1750660492:-1:-1;fr=0YNK0vbWcywFPoUUD.AWfl1BkVswHK71qLx72dWiHxy0UZZ1H0VHHYQx1-INHMYXRTp84.BoWPWS..AAA.0.0.BoWPWS.AWfLJ6MOKJ6Agl1B8t9vHhQqEjI;datr=i_VYaKu-Rzu_jd4dojbQWHNt;|EAAAAUaZA8jlABO31ZCsTti5tih5kYG5i148jrhMmjjt2bjcZBVLIcyU7yxE9bImIb7yBGb9V5FScK5TRihSNpDd7U87Qq9e128lgEWXp9ZACxx318oDwNIyWo0Eo3nLt3IQatM7iWrfjQ3dvTbAotZAxMCc40e0NxZCeteiLiogbrpTx1P4MhwW9ARf6QbV2RdwUmZBhuFK2AZDZD|uftmp7vbp9fl@mailto.plus'
]

const workbook = Workbook.create()
const sheet = workbook.worksheets.add('账号信息')
sheet.showGridLines = false

sheet.getRange('A1:G1').values = [[
  '原始数据（粘贴到此列）',
  'Facebook账号',
  '密码',
  '2FA密钥',
  'Cookie',
  'Access Token',
  '邮箱'
]]
sheet.getRange('A2:A3').values = rawRows.map((row) => [row])

// 兼容旧版 Excel：从 A 列按 | 提取第 N 个字段，向右、向下复制即可。
const splitFormula = '=IF($A2="","",IFERROR(TRIM(MID(SUBSTITUTE($A2,"|",REPT(" ",4096)),(COLUMNS($B2:B2)-1)*4096+1,4096)),""))'
sheet.getRange('B2').formulas = [[splitFormula]]
sheet.getRange('B2:G3').fillRight()
sheet.getRange('B2:G100').fillDown()

sheet.getRange('A5:G6').merge()
sheet.getRange('A5').values = [[
  '使用方法：以后把每个账号的完整信息按 | 分隔后粘贴到 A 列，B:G 会自动拆分。请勿把此文件分享给他人，里面包含登录凭据、Cookie 和密钥。'
]]

sheet.getRange('A1:G1').format = {
  fill: '#1F4E78',
  font: { bold: true, color: '#FFFFFF' },
  horizontalAlignment: 'center',
  verticalAlignment: 'center',
  wrapText: true,
  borders: { preset: 'all', style: 'thin', color: '#D9E2F3' }
}
sheet.getRange('A2:A100').format = {
  numberFormat: '@',
  verticalAlignment: 'top',
  wrapText: true
}
sheet.getRange('B2:G100').format = {
  numberFormat: '@',
  verticalAlignment: 'top',
  wrapText: true
}
sheet.getRange('A2:G3').format.borders = { preset: 'all', style: 'thin', color: '#D9D9D9' }
sheet.getRange('A5:G6').format = {
  fill: '#FFF2CC',
  font: { color: '#7F6000' },
  wrapText: true,
  verticalAlignment: 'center',
  borders: { preset: 'outside', style: 'thin', color: '#D6B656' }
}
sheet.getRange('A1:G1').format.rowHeight = 30
sheet.getRange('A2:A3').format.rowHeight = 100
sheet.getRange('A5:G6').format.rowHeight = 28
sheet.getRange('A:A').format.columnWidth = 55
sheet.getRange('B:B').format.columnWidth = 18
sheet.getRange('C:C').format.columnWidth = 16
sheet.getRange('D:D').format.columnWidth = 38
sheet.getRange('E:E').format.columnWidth = 65
sheet.getRange('F:F').format.columnWidth = 65
sheet.getRange('G:G').format.columnWidth = 30
sheet.freezePanes.freezeRows(1)
sheet.freezePanes.freezeColumns(1)

// 支持供应商把多个账号连续放在一行的格式（Excel 365/2021）。
const batchSheet = workbook.worksheets.add('整批粘贴')
batchSheet.showGridLines = false
batchSheet.getRange('A1:H1').values = [[
  '整段原始数据（粘贴到 A2）',
  '自动分出的账号行',
  'Facebook账号',
  '密码',
  '2FA密钥',
  'Cookie',
  'Access Token',
  '邮箱'
]]
batchSheet.getRange('A2').values = [[rawRows.join('| ')]]
batchSheet.getRange('B2').formulas = [[
  '=TEXTSPLIT(SUBSTITUTE(SUBSTITUTE($A$2,"| 615",CHAR(10)&"615"),"|615",CHAR(10)&"615"),,CHAR(10),TRUE)'
]]
const batchSplitFormula = '=IF($B2="","",IFERROR(TRIM(MID(SUBSTITUTE($B2,"|",REPT(" ",4096)),(COLUMNS($C2:C2)-1)*4096+1,4096)),""))'
batchSheet.getRange('C2').formulas = [[batchSplitFormula]]
batchSheet.getRange('C2:H100').fillRight()
batchSheet.getRange('C2:H100').fillDown()
batchSheet.getRange('A4:H5').merge()
batchSheet.getRange('A4').values = [[
  '以后把供应商发来的整段内容直接粘贴到 A2。B 列会按下一个 615 开头的账号自动分行，C:H 再按 | 拆分。此页需要 Excel 365/2021；旧版 Excel 请使用“账号信息”页，每个账号先单独放一行。'
]]
batchSheet.getRange('A1:H1').format = {
  fill: '#548235',
  font: { bold: true, color: '#FFFFFF' },
  horizontalAlignment: 'center',
  verticalAlignment: 'center',
  wrapText: true,
  borders: { preset: 'all', style: 'thin', color: '#D9EAD3' }
}
batchSheet.getRange('A2:B100').format = { numberFormat: '@', verticalAlignment: 'top', wrapText: true }
batchSheet.getRange('C2:H100').format = { numberFormat: '@', verticalAlignment: 'top', wrapText: true }
batchSheet.getRange('A4:H5').format = {
  fill: '#E2F0D9',
  font: { color: '#375623' },
  wrapText: true,
  verticalAlignment: 'center',
  borders: { preset: 'outside', style: 'thin', color: '#A9D18E' }
}
batchSheet.getRange('A1:H1').format.rowHeight = 30
batchSheet.getRange('A2').format.rowHeight = 100
batchSheet.getRange('A4:H5').format.rowHeight = 30
batchSheet.getRange('A:A').format.columnWidth = 55
batchSheet.getRange('B:B').format.columnWidth = 55
batchSheet.getRange('C:C').format.columnWidth = 18
batchSheet.getRange('D:D').format.columnWidth = 16
batchSheet.getRange('E:E').format.columnWidth = 38
batchSheet.getRange('F:F').format.columnWidth = 65
batchSheet.getRange('G:G').format.columnWidth = 65
batchSheet.getRange('H:H').format.columnWidth = 30
batchSheet.freezePanes.freezeRows(1)
batchSheet.freezePanes.freezeColumns(2)

const check = await workbook.inspect({
  kind: 'table',
  range: '账号信息!A1:G3',
  include: 'values,formulas',
  tableMaxRows: 3,
  tableMaxCols: 7,
  tableMaxCellChars: 100
})
console.log(check.ndjson)
console.log(JSON.stringify(sheet.getRange('B2:G3').formulas))

const errors = await workbook.inspect({
  kind: 'match',
  searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A',
  options: { useRegex: true, maxResults: 100 },
  summary: 'formula error scan'
})
console.log(errors.ndjson)

const preview = await workbook.render({ sheetName: '账号信息', range: 'A1:G6', scale: 1, format: 'png' })
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()))
const output = await SpreadsheetFile.exportXlsx(workbook)
await output.save(outputPath)
console.log(`SAVED ${outputPath}`)
