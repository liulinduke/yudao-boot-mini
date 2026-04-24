# Facebook转帖功能部署说明

## 功能概述

为Facebook运营模块新增了**转帖功能**,支持以下操作:
- ✅ 点赞
- ✅ 转发到动态  
- ✅ 转帖到个人中心(可配置数量)
- ✅ 转贴到好友
- ✅ 转发到群组(可配置数量,从已加组成功的群组中选择)
- ✅ 评论话术/话术库选择
- ✅ 任务历史查看

## 部署步骤

### 1. 执行数据库脚本

按顺序执行以下SQL脚本:

```bash
# 1. 创建转帖相关表和字段
mysql -u root -p your_database < sql/mysql/fb_repost.sql

# 2. 配置菜单权限(需要先确认父菜单ID)
mysql -u root -p your_database < sql/mysql/fb_operation_menu.sql
```

**注意**: `fb_operation_menu.sql`中的`@operation_menu_id`需要根据实际情况修改,通常为2050或查询system_menu表获取。

### 2. 后端代码

后端代码已全部生成,包括:

#### DO层
- `FbRepostResultDO.java` - 转帖结果实体
- `FbOperationTaskDetailDO.java` - 已更新,添加转帖相关字段

#### Mapper层
- `FbRepostResultMapper.java` - 转帖结果Mapper

#### Service层
- `FbOperationTaskService.java` - 已更新,添加`batchSaveRepostResult`方法
- `FbOperationTaskServiceImpl.java` - 已更新,实现转帖结果保存逻辑
- `FbOperationAddGroupResultService.java` - 加组结果Service接口(新建)
- `FbOperationAddGroupResultServiceImpl.java` - 加组结果Service实现(新建)

#### Controller层
- `FbOperationTaskController.java` - 已更新,添加转帖结果保存接口
- `FbOperationAddGroupResultController.java` - 加组结果Controller(新建)

#### VO层
- `FbRepostResultBatchSaveReqVO.java` - 转帖结果批量保存请求VO
- `FbOperationAddGroupResultPageReqVO.java` - 加组结果分页请求VO
- `FbOperationAddGroupResultRespVO.java` - 加组结果响应VO

### 3. 前端代码

前端代码已全部生成,包括:

#### API层
- `src/api/facebook/operation/index.ts` - 已更新,添加转帖相关接口
- `src/api/facebook/operation/addgroupresult.ts` - 加组结果API(新建)
- `src/api/facebook/script.ts` - 话术API(新建)

#### 页面组件
- `src/views/facebook/operation/RepostForm.vue` - 转帖表单组件(新建)
- `src/views/facebook/operation/GroupSelectorForRepost.vue` - 群组选择器(新建)
- `src/views/facebook/operation/index.vue` - 已更新,启用转帖功能
- `src/views/facebook/operation/FbOperationForm.vue` - 已更新,添加转帖结果展示Tab

### 4. 重启服务

```bash
# 重启后端服务
cd yudao-server
mvn spring-boot:run

# 重启前端服务(如果正在运行)
cd yudao-ui/yudao-ui-admin-vue3
npm run dev
```

## 使用说明

### 创建转帖任务

1. 进入 **Facebook > 运营任务** 菜单
2. 点击左侧 **转贴** 卡片
3. 填写转帖表单:
   - **帖子链接**: 输入要转发的Facebook帖子URL
   - **执行账号**: 多选执行账号
   - **执行项**: 
     - ☑️ 点赞
     - ☑️ 转发到动态
     - ☑️ 转帖到个人中心 (右侧显示数量输入框,默认1)
     - ☑️ 转贴到好友
     - ☑️ 转发到群组 (右侧显示数量输入框+选择群组按钮)
   - **评论话术**: 可选,直接输入或使用话术库
   - **话术库**: 可选,从话术库选择
4. 点击确定创建任务

### 选择群组

1. 勾选"转发到群组"执行项
2. 点击"选择群组"按钮
3. 在弹出的对话框中:
   - 系统自动加载所有**加组成功**的群组(来自`fb_operation_add_group_result`表)
   - 支持按群组名称搜索
   - 支持多选
4. 点击确定

### 查看任务历史

1. 在任务列表中找到转帖任务(taskType=2)
2. 点击"详情"按钮
3. 切换到"🔄 转帖结果"Tab
4. 查看每条转帖记录的:
   - 操作类型(点赞/转发到动态/转帖到个人中心等)
   - 目标名称和链接
   - 执行状态
   - 失败原因
   - 执行时间

## 技术细节

### 数据库设计

#### fb_repost_result (转帖结果表)
```sql
- id: 结果ID
- detail_id: 任务明细ID
- task_id: 任务ID
- account_id: Facebook账号ID
- fb_account: Facebook账号
- post_url: 原帖子链接
- action_type: 操作类型(1-点赞 2-转发到动态 3-转帖到个人中心 4-转贴到好友 5-转发到群组)
- target_type: 目标类型(friend/group)
- target_id: 目标ID
- target_name: 目标名称
- target_url: 目标链接
- status: 状态(0-待处理 1-成功 2-失败)
- fail_reason: 失败原因
- execute_time: 执行时间
```

#### fb_operation_task_detail (任务明细表扩展)
新增字段:
- `post_url`: 帖子链接
- `action_config`: 执行项配置(JSON格式)
- `comment_script`: 评论话术
- `script_library_id`: 话术库ID

### API接口

#### 后端接口
- `POST /facebook/fb-operation-task/create` - 创建转帖任务
- `POST /facebook/fb-operation-task/batch-save-repost-result` - 批量保存转帖结果
- `GET /facebook/fb-operation-add-group-result/page` - 查询加组结果分页(用于群组选择)
- `GET /facebook/fb-script/page` - 查询话术列表

#### 前端调用示例
```typescript
// 创建转帖任务
await createFbOperationTask({
  taskType: 2,
  accountIds: ['account1', 'account2'],
  postUrl: 'https://www.facebook.com/xxx/posts/123',
  actionConfig: JSON.stringify({
    actions: [1, 2, 3, 5],
    shareToProfileCount: 2,
    shareToGroupCount: 3,
    selectedGroups: [...]
  }),
  commentScript: '评论内容',
  scriptLibraryId: 1,
  expectedCount: 100
})

// 批量保存转帖结果
await batchSaveRepostResult({
  detailId: 123,
  results: [
    {
      accountId: '10001234567890',
      actionType: 5,
      targetType: 'group',
      targetId: '987654321',
      targetName: '测试群组',
      status: 1
    }
  ]
})
```

## 注意事项

1. **风控提示**: 建议每个账号每日转帖操作不超过20次,避免触发风控机制
2. **群组选择**: 只能选择加组成功的群组(joinStatus=1或3)
3. **期望数量计算**: 
   - 点赞: 账号数 × 1
   - 转发到动态: 账号数 × 1
   - 转帖到个人中心: 账号数 × 配置数量
   - 转贴到好友: 账号数 × 10(估算)
   - 转发到群组: 账号数 × 选择的群组数
4. **话术库**: 需要先创建话术库功能才有数据可选

## 文件清单

### 后端文件 (15个)
1. `FbRepostResultDO.java`
2. `FbRepostResultMapper.java`
3. `FbOperationTaskService.java` (更新)
4. `FbOperationTaskServiceImpl.java` (更新)
5. `FbOperationAddGroupResultService.java`
6. `FbOperationAddGroupResultServiceImpl.java`
7. `FbOperationTaskController.java` (更新)
8. `FbOperationAddGroupResultController.java`
9. `FbRepostResultBatchSaveReqVO.java`
10. `FbOperationAddGroupResultPageReqVO.java`
11. `FbOperationAddGroupResultRespVO.java`
12. `FbOperationTaskDetailDO.java` (更新)

### 前端文件 (8个)
1. `src/api/facebook/operation/index.ts` (更新)
2. `src/api/facebook/operation/addgroupresult.ts`
3. `src/api/facebook/script.ts`
4. `src/views/facebook/operation/RepostForm.vue`
5. `src/views/facebook/operation/GroupSelectorForRepost.vue`
6. `src/views/facebook/operation/index.vue` (更新)
7. `src/views/facebook/operation/FbOperationForm.vue` (更新)

### SQL文件 (2个)
1. `sql/mysql/fb_repost.sql`
2. `sql/mysql/fb_operation_menu.sql`

## 后续优化建议

1. 实现真正的文件上传功能(目前附件只是JSON字符串)
2. 添加转帖模板变量替换功能
3. 支持定时转帖任务
4. 添加转帖频率控制
5. 实现智能话术推荐
6. 添加转帖效果统计分析
