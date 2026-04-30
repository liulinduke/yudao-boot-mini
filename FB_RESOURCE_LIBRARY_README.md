# FB资源库功能说明

## 功能概述

在Facebook一级菜单下新增"FB资源库"二级菜单，用于统一管理采集到的三种数据：
1. **潜客（用户）** - 来自 `fb_collect_user` 表
2. **群组** - 来自 `fb_collect_group` 表
3. **帖子** - 来自 `fb_collect_post` 表

采用Tab切换的方式在一个界面上查看和管理这三种数据。

## 部署步骤

### 1. 执行菜单SQL

```bash
# 在MySQL中执行
mysql -u root -p your_database < sql/mysql/fb_resource_menu.sql
```

或者手动在数据库管理工具中执行 `sql/mysql/fb_resource_menu.sql` 文件。

### 2. 前端代码已就绪

前端页面已经创建完成：
- 页面路径：`yudao-ui/yudao-ui-admin-vue3/src/views/facebook/resource/index.vue`
- 路由路径：`/facebook/resource`
- 组件名称：`FacebookResource`

### 3. 重新编译前端（如需要）

```bash
cd yudao-ui/yudao-ui-admin-vue3
npm run build:prod
```

### 4. 刷新浏览器

登录系统后，刷新浏览器即可在Facebook菜单下看到"FB资源库"菜单项。

## 功能特性

### 潜客（用户）管理
- ✅ 分页查询
- ✅ 按用户名、数据来源筛选
- ✅ 按同步时间范围筛选
- ✅ 单条删除
- ✅ 批量删除
- ✅ 导出Excel

### 群组管理
- ✅ 分页查询
- ✅ 按群组名称筛选
- ✅ 按成员数量范围筛选
- ✅ 单条删除
- ✅ 批量删除
- ✅ 导出Excel

### 帖子管理
- ✅ 分页查询
- ✅ 按发帖人、群组名称、帖子内容筛选
- ✅ 按互动数据（点赞/评论/转发）筛选
- ✅ 单条删除
- ✅ 批量删除
- ✅ 导出Excel

## 权限配置

系统自动创建了以下权限：

### 主权限
- `facebook:resource:query` - 资源库查询
- `facebook:resource:delete` - 资源库删除
- `facebook:resource:export` - 资源库导出

### 细分权限（可选使用）
- 潜客：`facebook:fb-collect-user:query/delete/export`
- 群组：`facebook:fb-collect-group:query/delete/export`
- 帖子：`facebook:fb-collect-post:query/delete/export`

请在角色管理中为相应角色分配这些权限。

## 技术实现

### 后端
- 复用现有的Controller接口
- 无需修改后端代码

### 前端
- 使用Element Plus的Tabs组件实现Tab切换
- 每个Tab独立维护查询参数和数据列表
- 懒加载：只在首次切换到某个Tab时加载数据
- 响应式设计，支持各种屏幕尺寸

## 注意事项

1. **菜单ID冲突**：如果系统中已存在ID为2050的菜单，需要修改SQL中的parent_id
2. **权限控制**：确保为用户分配了相应的权限才能看到菜单和操作按钮
3. **数据量**：如果数据量很大，建议添加更多筛选条件以提高查询性能
4. **导出功能**：导出时会忽略分页，导出所有符合条件的数据

## 后续优化建议

1. 可以添加更多筛选条件（如地区、性别等）
2. 可以添加数据统计图表（如采集趋势、分布等）
3. 可以添加数据去重功能
4. 可以添加数据标记/分类功能
5. 可以添加批量操作（如批量分配分组）

## 常见问题

**Q: 菜单不显示？**
A: 检查是否执行了SQL，并确认当前用户角色是否有权限。

**Q: 点击菜单报错？**
A: 检查前端是否正确编译，清除浏览器缓存后重试。

**Q: 导出数据为空？**
A: 检查是否有符合筛选条件的数据，或调整筛选条件。

## 联系支持

如有问题，请联系开发团队。
