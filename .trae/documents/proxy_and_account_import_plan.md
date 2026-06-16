
# 代理管理与FB账号导入功能实现计划

## 需求分析

根据用户需求和竞品B分析，需要实现以下三个功能：

| 序号 | 需求 | 描述 |
|------|------|------|
| 1 | 代理管理 | 在**系统设置**中添加代理管理模块（通用模块，非FB专用），包含代理的CRUD操作 |
| 2 | FB账号代理关联 | 在FB账号管理中添加代理ID字段，并实现批量修改代理功能 |
| 3 | FB账号导入 | 添加账号导入和Cookie导入功能，格式参考竞品B |

## 竞品B分析总结

### 账号导入特性
1. **输入格式**（TXT文本）：
   - `Facebook用户名----Facebook密码`
   - `Facebook用户名----Facebook密码----双重验证安全码`
2. **两步导入流程**：
   - 第一步：输入文本内容
   - 第二步：预览表格，设置分组和代理，确认导入
3. **验证规则**：
   - 用户名不能为空
   - 密码不能为空
   - 用户名不能重复

### Cookie导入特性
1. **输入格式**（TXT文本）：
   - 多条Cookie请换行
   - 支持字符串格式、JSON格式
2. **两步导入流程**：
   - 第一步：输入Cookie文本
   - 第二步：预览表格，设置分组和代理
3. **验证规则**：
   - Cookie必须有效
   - 从Cookie中提取用户ID
   - 用户ID不能重复

### 通用特性
- 导入前可设置分组（下拉选择）
- 导入前可设置代理（下拉选择）

## 技术方案

### 1. 代理管理模块（系统设置）

#### 数据库设计 (`sys_proxy`)

| 字段名 | 类型 | 说明 |
|--------|------|------|
| id | BIGINT | 主键，自增 |
| proxy_name | VARCHAR(100) | 代理名称 |
| proxy_type | INT | 代理类型（1-HTTP, 2-HTTPS, 3-SOCKS5） |
| host | VARCHAR(255) | 代理服务器地址 |
| port | INT | 代理端口 |
| username | VARCHAR(100) | 代理认证用户名 |
| password | VARCHAR(255) | 代理认证密码（加密存储） |
| country | VARCHAR(50) | 国家/地区 |
| status | INT | 状态（0-禁用，1-启用） |
| remark | VARCHAR(500) | 备注 |
| create_time | DATETIME | 创建时间 |
| update_time | DATETIME | 更新时间 |

#### API设计

| 接口 | 方法 | 路径 | 说明 |
|------|------|------|------|
| 分页查询 | GET | `/system/proxy/page` | 查询代理列表 |
| 新增代理 | POST | `/system/proxy/create` | 创建新代理 |
| 修改代理 | PUT | `/system/proxy/update` | 更新代理信息 |
| 删除代理 | DELETE | `/system/proxy/delete/{id}` | 删除代理 |
| 获取详情 | GET | `/system/proxy/get/{id}` | 获取代理详情 |
| 获取列表 | GET | `/system/proxy/list` | 获取所有启用的代理列表 |

### 2. FB账号代理关联

#### 修改 `fb_account` 表

| 字段名 | 类型 | 说明 |
|--------|------|------|
| proxy_id | BIGINT | 代理ID（外键关联 sys_proxy.id） |

#### API设计

| 接口 | 方法 | 路径 | 说明 |
|------|------|------|------|
| 批量修改代理 | PUT | `/facebook/fb-account/batch-update-proxy` | 批量更新账号的代理ID |

### 3. FB账号导入功能

#### 导入格式设计（参考竞品B）

**账号导入格式（TXT）：**
```
Facebook用户名----Facebook密码
Facebook用户名----Facebook密码----双重验证安全码
```

| 字段 | 说明 | 必填 |
|------|------|------|
| fbAccount | FB账号 | 是 |
| password | 密码 | 是 |
| tfa | 双重验证安全码 | 否 |

**Cookie导入格式（TXT）：**
```
cookie_string_or_json_1
cookie_string_or_json_2
cookie_string_or_json_3
```
- 多条Cookie请换行
- 支持字符串格式和JSON格式

#### API设计

| 接口 | 方法 | 路径 | 说明 |
|------|------|------|------|
| 导入账号 | POST | `/facebook/fb-account/import` | 导入账号列表（含分组、代理设置） |
| 导入Cookie | POST | `/facebook/fb-account/import-cookie` | 导入Cookie（含分组、代理设置） |

#### 导入请求参数

| 字段 | 类型 | 说明 |
|------|------|------|
| data | String | 导入的文本数据 |
| groupId | Long | 分组ID（可选） |
| proxyId | Long | 代理ID（可选） |

## 文件结构

### 后端文件

```
yudao-module-system/
├── src/main/java/cn/iocoder/yudao/module/system/
│   ├── controller/admin/proxy/
│   │   ├── SysProxyController.java      # 代理管理控制器
│   │   └── vo/
│   │       ├── SysProxyCreateReqVO.java  # 创建请求
│   │       ├── SysProxyUpdateReqVO.java  # 更新请求
│   │       ├── SysProxyRespVO.java       # 响应VO
│   │       └── SysProxyPageReqVO.java    # 分页请求
│   ├── service/proxy/
│   │   ├── SysProxyService.java          # 服务接口
│   │   └── impl/
│   │       └── SysProxyServiceImpl.java  # 服务实现
│   ├── dal/
│   │   ├── dataobject/
│   │   │   └── SysProxyDO.java           # 数据对象
│   │   └── mapper/
│   │       └── SysProxyMapper.java       # Mapper接口

yudao-module-facebook/
├── src/main/java/cn/iocoder/yudao/module/facebook/
│   └── controller/admin/account/
│       └── FbAccountController.java     # 修改：添加批量修改代理、导入接口
```

### 前端文件

```
yudao-ui-admin-vue3/
├── src/
│   ├── api/system/
│   │   └── proxy.ts                     # 代理管理API
│   ├── views/system/
│   │   └── proxy/
│   │       ├── index.vue                # 代理管理列表页
│   │       └── SysProxyForm.vue         # 代理表单
│   └── views/facebook/
│       └── account/
│           ├── index.vue                # 修改：添加批量修改代理、导入下拉菜单
│           ├── FbAccountForm.vue        # 修改：添加代理选择器
│           └── ImportDialog.vue         # 导入弹窗（支持账号导入和Cookie导入）
```

## 实现步骤

### 阶段一：代理管理模块（后端）

1. 创建 `SysProxyDO.java` 数据对象
2. 创建 `SysProxyMapper.java` Mapper接口
3. 创建 `SysProxyService.java` 服务接口
4. 创建 `SysProxyServiceImpl.java` 服务实现
5. 创建 Controller 和 VO 类
6. 添加 MyBatis XML 配置
7. 添加数据库迁移脚本

### 阶段二：代理管理模块（前端）

1. 创建代理管理列表页 `index.vue`
2. 创建代理表单 `SysProxyForm.vue`
3. 创建 API 文件 `proxy.ts`
4. 添加路由配置（系统设置菜单下）

### 阶段三：FB账号代理关联

1. 修改 `FbAccountDO.java` 添加 `proxyId` 字段
2. 修改 `FbAccountRespVO.java` 添加 `proxyId` 和 `proxyName`
3. 修改 `FbAccountMapper.java` 添加关联查询
4. 添加批量修改代理 API
5. 修改前端列表页添加批量修改代理按钮
6. 修改前端表单添加代理选择器

### 阶段四：FB账号导入功能

1. 添加导入 API（账号导入、Cookie导入）
2. 创建导入弹窗组件（支持两步导入流程）
   - 第一步：文本输入
   - 第二步：预览表格 + 设置分组/代理
3. 修改列表页添加导入下拉菜单（账号导入、Cookie导入）

## 风险评估

| 风险 | 描述 | 影响 | 应对措施 |
|------|------|------|----------|
| 数据库迁移 | 添加新表和字段需要数据库迁移 | 可能影响现有数据 | 使用 Liquibase 进行增量迁移 |
| 导入数据验证 | 导入格式错误可能导致数据异常 | 数据完整性风险 | 添加严格的导入校验和错误提示 |
| 代理密码安全 | 代理密码明文存储有安全风险 | 信息泄露 | 使用AES加密存储密码 |
| 批量操作性能 | 批量修改代理可能影响大量数据 | 性能问题 | 使用批量更新语句，添加事务控制 |

## 依赖关系

- 后端：Spring Boot 3.x + MyBatis Plus
- 前端：Vue 3 + Element Plus + TypeScript
- 数据库：MySQL 8.x

## 时间预估

| 阶段 | 预估时间 |
|------|----------|
| 代理管理后端 | 2天 |
| 代理管理前端 | 1.5天 |
| FB账号代理关联 | 1天 |
| FB账号导入功能 | 2天 |
| 测试和调试 | 1天 |
| **总计** | **7.5天** |
