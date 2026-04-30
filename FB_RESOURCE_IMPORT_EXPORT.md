# FB资源库导入导出功能说明

## 功能概述

FB资源库现已完整支持数据的导入和导出功能：

### 导出功能（全部三种数据）
- ✅ **潜客（用户）** - 支持导出Excel
- ✅ **群组** - 支持导出Excel  
- ✅ **帖子** - 支持导出Excel

### 导入功能
- ✅ **帖子** - 支持从Excel导入（带模板下载）

## 已完成的修改

### 1. API层修改

#### 用户API (`src/api/facebook/collectuser/index.ts`)
```typescript
// 导出方法名统一为 exportFbCollectUserExcel
exportFbCollectUserExcel: async (params) => {
  return await request.download({ url: `/facebook/fb-collect-user/export-excel`, params })
}
```

#### 群组API (`src/api/facebook/fbcollectgroup/index.ts`)
```typescript
// 导出方法名统一为 exportFbCollectGroupExcel
exportFbCollectGroupExcel: async (params) => {
  return await request.download({ url: `/facebook/fb-collect-group/export-excel`, params })
}
```

#### 帖子API (`src/api/facebook/fbcollectpost/index.ts`)
```typescript
// 导出方法
exportFbCollectPostExcel: async (params) => {
  return await request.download({ url: `/facebook/fb-collect-post/export-excel`, params })
},

// 新增：下载导入模板
importFbCollectPostTemplate: async () => {
  return await request.download({ url: `/facebook/fb-collect-post/get-import-template` })
}
```

### 2. 前端组件

#### 帖子导入组件 (`src/views/facebook/resource/components/PostImportForm.vue`)
- 文件上传界面
- 模板下载功能
- 导入结果展示
- 错误处理

主要特性：
- 支持拖拽上传
- 限制只能上传一个文件
- 支持 .xls 和 .xlsx 格式
- 自动添加认证头（Token + Tenant-ID）
- 显示详细的导入结果（成功数、失败数、失败原因）

### 3. 主页面更新 (`src/views/facebook/resource/index.vue`)

在帖子Tab的搜索栏中添加了"导入"按钮：
```vue
<el-button
  type="primary"
  plain
  @click="openPostImport"
  v-hasPermi="['facebook:fb-collect-post:create']"
>
  <Icon icon="ep:upload" class="mr-5px" /> 导入
</el-button>
```

## 使用方法

### 导出数据

1. **潜客数据导出**
   - 切换到"潜客（用户）"Tab
   - 设置筛选条件（可选）
   - 点击"导出"按钮
   - 确认导出操作
   - 浏览器自动下载Excel文件

2. **群组数据导出**
   - 切换到"群组"Tab
   - 设置筛选条件（可选）
   - 点击"导出"按钮
   - 确认导出操作
   - 浏览器自动下载Excel文件

3. **帖子数据导出**
   - 切换到"帖子"Tab
   - 设置筛选条件（可选）
   - 点击"导出"按钮
   - 确认导出操作
   - 浏览器自动下载Excel文件

### 导入帖子数据

1. **下载模板**
   - 切换到“帖子”Tab
   - 点击“导入”按钮
   - 在弹窗中点击“下载模板”链接
   - 保存模板文件到本地

2. **填写模板**
   - 打开下载的Excel模板
   - **只需填写一列：帖子URL**
   - 示例：`https://www.facebook.com/groups/xxx/posts/yyy`
   - 每行一个URL
   - 保存文件

3. **上传导入**
   - 点击“导入”按钮
   - 将填写好的Excel文件拖拽到上传区域，或点击选择文件
   - 点击“确定”按钮开始上传
   - 系统会将URL批量保存到数据库
   - 等待导入完成

4. **查看结果**
   - 系统会显示导入结果弹窗
   - 包含：成功数量、失败数量
   - 如果有失败记录，会显示详细失败原因（如：URL格式错误、重复数据等）
   - 点击确认后，列表会自动刷新
   - **注意**：导入的URL需要后续通过采集任务抓取详细信息

## 后端接口需求

### 需要实现的后端接口

#### 1. 帖子导入模板下载
```java
GET /facebook/fb-collect-post/get-import-template
返回：Excel文件流（只有一列：url）
```

**模板格式示例：**
```
| url |
|-----|
| https://www.facebook.com/groups/123/posts/456 |
| https://www.facebook.com/groups/789/posts/012 |
```

#### 2. 帖子数据导入
```java
POST /facebook/fb-collect-post/import
请求：multipart/form-data，包含Excel文件
响应：{
  code: 0,
  data: {
    successCount: 100,      // 成功抓取的数量
    failureCount: 5,        // 失败的数量
    failureMessages: [      // 失败详情
      "第3行：URL格式错误",
      "第8行：帖子不存在或无权限访问",
      ...
    ]
  }
}
```

**后端处理逻辑：**
```java
1. 读取Excel中的URL列表
2. 对每个URL：
   a. 验证URL格式
   b. 检查是否已存在（避免重复）
   c. 插入到 fb_collect_post 表（只保存url字段）
3. 返回导入结果统计

注意：导入只保存URL，不抓取数据。
后续需要通过采集任务去抓取这些URL的详细信息。

### 参考实现

可以参考系统中已有的导入实现：
- `SystemUserController.importUserTemplate()` - 下载模板
- `SystemUserController.importUser()` - 导入数据

关键代码模式：
```java
@GetMapping("/get-import-template")
@Operation(summary = "下载导入模板")
public void importTemplate(HttpServletResponse response) throws IOException {
    // 使用 ExcelUtils 生成模板
    ExcelUtils.write(response, "帖子导入模板.xls", "帖子数据", FbCollectPostImportVO.class, 
                     Collections.emptyList());
}

@PostMapping("/import")
@Operation(summary = "导入帖子数据")
public CommonResult<FbCollectPostImportRespVO> importData(
        @RequestParam("file") MultipartFile file) throws Exception {
    // 读取Excel
    List<FbCollectPostImportVO> list = ExcelUtils.read(file, FbCollectPostImportVO.class);
    
    // 处理导入逻辑
    // ...
    
    // 返回结果
    return success(result);
}
```

## 权限配置

确保为用户分配以下权限：

### 导出权限
- `facebook:fb-collect-user:export` - 潜客导出
- `facebook:fb-collect-group:export` - 群组导出
- `facebook:fb-collect-post:export` - 帖子导出

### 导入权限
- `facebook:fb-collect-post:create` - 帖子导入（使用创建权限）

## 注意事项

1. **文件格式**
   - 只支持 .xls 和 .xlsx 格式
   - Excel只需一列：url
   - 文件大小建议不超过10MB
   - 单次导入建议不超过5000条（只是保存URL，速度很快）

2. **数据验证**
   - 导入时会进行URL格式验证
   - 检查是否重复（根据url字段）
   - 无效的URL会被跳过并记录错误信息
   - 建议先用小批量数据测试（10-20条）

3. **重复数据处理**
   - 系统会根据URL检查是否已存在
   - 重复的URL会被跳过

## 后续扩展建议

### 可以为其他数据类型添加导入功能

1. **潜客（用户）导入**
   - 复制 `PostImportForm.vue` 改为 `UserImportForm.vue`
   - 修改API调用和字段映射
   - 在主页面添加导入按钮

2. **群组导入**
   - 同样的方式创建 `GroupImportForm.vue`
   - 添加对应的后端接口

### 增强功能

1. **导入预览**
   - 上传后先预览数据
   - 允许用户在导入前修改或删除某些行

2. **导入历史记录**
   - 记录每次导入的时间、数量、操作人
   - 提供导入历史查询功能

3. **智能匹配**
   - 导入时自动匹配现有数据
   - 提供去重和合并选项

4. **批量操作**
   - 导入后自动分配到指定分组
   - 批量标记数据来源

## 故障排查

### 问题1：点击导入按钮无反应
- 检查是否有 `facebook:fb-collect-post:create` 权限
- 检查浏览器控制台是否有JavaScript错误
- 确认组件是否正确引用

### 问题2：上传失败
- 检查网络连接
- 确认后端接口是否正常运行
- 检查Token是否有效
- 查看浏览器Network面板的请求详情

### 问题3：导入结果为空或数量不对
- 检查Excel文件格式是否正确
- 确认使用了最新的模板
- 检查URL格式是否正确
- 查看是否有重复URL被跳过
- 查看后端日志中的错误信息

### 问题4：导出的Excel打不开
- 确认使用的是Microsoft Excel或WPS
- 检查文件扩展名是否正确
- 尝试用其他Excel软件打开

## 技术支持

如有问题，请检查：
1. 浏览器控制台错误信息
2. Network面板的API请求和响应
3. 后端日志文件
4. 数据库中的数据状态

---

**版本**: v1.0  
**更新日期**: 2026-04-30  
**作者**: AI Assistant
