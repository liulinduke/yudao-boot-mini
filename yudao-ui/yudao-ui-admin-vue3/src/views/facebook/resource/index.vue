<template>
  <ContentWrap>
    <!-- Tab切换 -->
    <el-tabs v-model="activeTab" type="card" class="mb-20px">
      <el-tab-pane label="潜客" name="user">
        <template #label>
          <span class="tab-label">
            <Icon icon="ep:user-filled" class="mr-5px" />
            潜客
          </span>
        </template>

        <!-- 搜索栏 -->
        <el-form
          class="search-form mb-16px"
          :model="userQueryParams"
          ref="userQueryFormRef"
          :inline="true"
          label-width="80px"
        >
          <el-form-item label="用户名" prop="userName">
            <el-input
              v-model="userQueryParams.userName"
              placeholder="请输入用户名"
              clearable
              @keyup.enter="handleUserQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="数据来源" prop="fromResource">
            <el-input
              v-model="userQueryParams.fromResource"
              placeholder="请输入数据来源"
              clearable
              @keyup.enter="handleUserQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="分组">
            <ResourceGroupControl v-model="userQueryParams.resourceGroupId" resource-type="LEAD" title="潜客分组" @change="handleUserQuery" />
          </el-form-item>
          <el-form-item label="深度采集" prop="deepCollected">
            <el-select
              v-model="userQueryParams.deepCollected"
              placeholder="是否深度采集"
              clearable
              class="!w-150px"
            >
              <el-option label="已采集" :value="true" />
              <el-option label="未采集" :value="false" />
            </el-select>
          </el-form-item>
          <el-form-item label="AI标签" prop="aiTags">
            <el-select v-model="userQueryParams.aiTags" placeholder="请选择AI标签" clearable class="!w-160px">
              <el-option v-for="tag in aiTagOptions" :key="tag" :label="tag" :value="tag" />
            </el-select>
          </el-form-item>
          <el-form-item label="意向等级" prop="intentLevel">
            <el-select v-model="userQueryParams.intentLevel" placeholder="请选择意向" clearable class="!w-140px">
              <el-option label="高" value="high" />
              <el-option label="中" value="medium" />
              <el-option label="低" value="low" />
              <el-option label="未知" value="unknown" />
            </el-select>
          </el-form-item>
          <el-form-item label="触达状态" prop="touchStatus">
            <el-select v-model="userQueryParams.touchStatus" placeholder="请选择状态" clearable class="!w-150px">
              <el-option label="未触达" value="not_touched" />
              <el-option label="已触达" value="touched" />
              <el-option label="已回复" value="replied" />
              <el-option label="已完成" value="done" />
            </el-select>
          </el-form-item>
          <el-form-item label="采集时间" prop="createTime">
            <el-date-picker
              v-model="userQueryParams.createTime"
              value-format="YYYY-MM-DD HH:mm:ss"
              type="daterange"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
              class="!w-240px"
            />
          </el-form-item>
          <el-form-item>
            <el-button @click="handleUserQuery">
              <Icon icon="ep:search" class="mr-5px" /> 搜索
            </el-button>
            <el-button @click="resetUserQuery">
              <Icon icon="ep:refresh" class="mr-5px" /> 重置
            </el-button>
            <el-button
              type="danger"
              plain
              :disabled="isEmpty(userCheckedIds)"
              @click="handleUserDeleteBatch"
              v-hasPermi="['facebook:fb-collect-user:delete']"
            >
              <Icon icon="ep:delete" class="mr-5px" /> 批量删除
            </el-button>
            <el-button
              type="success"
              plain
              @click="handleUserExport"
              v-hasPermi="['facebook:fb-collect-user:export']"
            >
              <Icon icon="ep:download" class="mr-5px" /> 导出
            </el-button>
          </el-form-item>
        </el-form>

        <!-- 用户列表 -->
        <el-table
          row-key="id"
          v-loading="userLoading"
          :data="userList"
          :stripe="true"
          :show-overflow-tooltip="true"
          @selection-change="handleUserRowCheckboxChange"
        >
          <el-table-column type="selection" width="55" />
          <el-table-column label="ID" align="center" prop="id" width="80" />
          <el-table-column label="采集时间" align="center" prop="createTime" width="160">
            <template #default="scope">
              {{ formatDateTime(scope.row.createTime) }}
            </template>
          </el-table-column>
          <el-table-column label="分组" align="center" prop="resourceGroupId" width="110">
            <template #default="scope">{{ getResourceGroupName(scope.row.resourceGroupId) }}</template>
          </el-table-column>
          <el-table-column
            label="用户名"
            align="center"
            prop="userName"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column
            label="用户ID"
            align="center"
            prop="userId"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column
            label="主页链接"
            align="center"
            prop="url"
            min-width="200"
            show-overflow-tooltip
          >
            <template #default="scope">
              <el-link :href="scope.row.url" target="_blank" type="primary" v-if="scope.row.url">
                {{ scope.row.url }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="好友数" align="center" prop="friendCount" width="100" />
          <el-table-column label="粉丝数" align="center" prop="followerCount" width="100" />
          <el-table-column label="关注数" align="center" prop="followingCount" width="100" />
          <el-table-column label="深度采集" align="center" prop="deepCollected" width="100">
            <template #default="scope">
              <el-tag :type="scope.row.deepCollected ? 'success' : 'info'">
                {{ scope.row.deepCollected ? '已采集' : '未采集' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="AI标签" align="center" prop="aiTags" width="180">
            <template #default="scope">
              <div class="tag-list" v-if="splitTags(scope.row.aiTags).length">
                <el-tag v-for="tag in splitTags(scope.row.aiTags)" :key="tag" size="small" class="mr-4px">
                  {{ tag }}
                </el-tag>
              </div>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="意向" align="center" prop="intentLevel" width="90">
            <template #default="scope">
              <el-tag :type="getIntentTagType(scope.row.intentLevel)">
                {{ getIntentLabel(scope.row.intentLevel) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="相关度" align="center" prop="productRelevanceScore" width="90">
            <template #default="scope">
              {{ scope.row.productRelevanceScore ?? '-' }}
            </template>
          </el-table-column>
          <el-table-column label="触达" align="center" prop="touchStatus" width="100">
            <template #default="scope">
              <el-tag :type="getTouchTagType(scope.row.touchStatus)">
                {{ getTouchLabel(scope.row.touchStatus) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="电话" align="center" prop="phonenumber" width="130" />
          <el-table-column label="WhatsApp" align="center" prop="whatsapp" width="130" />
          <el-table-column label="Line" align="center" prop="line" width="120" />
          <el-table-column label="邮箱" align="center" prop="email" width="180" />
          <el-table-column label="网站" align="center" prop="website" width="180" show-overflow-tooltip />
          <el-table-column label="类别" align="center" prop="category" width="140" show-overflow-tooltip />
          <el-table-column label="简介/状态" align="center" prop="profileStatus" width="180" show-overflow-tooltip />
          <el-table-column label="所在地" align="center" prop="city" width="120" />
          <el-table-column label="居住地" align="center" prop="location" width="120" />
          <el-table-column label="性别" align="center" prop="gender" width="90" />
          <el-table-column label="最近帖子摘要" align="center" prop="lastPostSummary" width="220" show-overflow-tooltip />
          <el-table-column label="AI摘要" align="center" prop="aiSummary" width="220" show-overflow-tooltip />
          <el-table-column label="最近发帖" align="center" prop="lastPostTime" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.lastPostTime) }}
            </template>
          </el-table-column>
          <el-table-column
            label="数据来源"
            align="center"
            prop="fromResource"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column label="操作" align="center" width="120" fixed="right">
            <template #default="scope">
              <el-button
                link
                type="danger"
                @click="handleUserDelete(scope.row.id)"
                v-hasPermi="['facebook:fb-collect-user:delete']"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页 -->
        <Pagination
          :total="userTotal"
          v-model:page="userQueryParams.pageNo"
          v-model:limit="userQueryParams.pageSize"
          @pagination="getUserList"
        />
      </el-tab-pane>

      <el-tab-pane label="群组" name="group">
        <template #label>
          <span class="tab-label">
            <Icon icon="ep:user" class="mr-5px" />
            群组
          </span>
        </template>

        <!-- 搜索栏 -->
        <el-form
          class="search-form mb-16px"
          :model="groupQueryParams"
          ref="groupQueryFormRef"
          :inline="true"
          label-width="80px"
        >
          <el-form-item label="群组名称" prop="groupName">
            <el-input
              v-model="groupQueryParams.groupName"
              placeholder="请输入群组名称"
              clearable
              @keyup.enter="handleGroupQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="分组">
            <ResourceGroupControl v-model="groupQueryParams.resourceGroupId" resource-type="GROUP" title="群组分组" @change="handleGroupQuery" />
          </el-form-item>
          <el-form-item label="成员数量" prop="memberQuantity">
            <el-input-number
              v-model="groupQueryParams.minMemberQuantity"
              placeholder="最小成员数"
              :min="0"
              class="!w-120px"
            />
            <span class="mx-8px">-</span>
            <el-input-number
              v-model="groupQueryParams.maxMemberQuantity"
              placeholder="最大成员数"
              :min="0"
              class="!w-120px"
            />
          </el-form-item>
          <el-form-item label="采集时间" prop="createTime">
            <el-date-picker
              v-model="groupQueryParams.createTime"
              value-format="YYYY-MM-DD HH:mm:ss"
              type="daterange"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
              class="!w-240px"
            />
          </el-form-item>
          <el-form-item>
            <el-button @click="handleGroupQuery">
              <Icon icon="ep:search" class="mr-5px" /> 搜索
            </el-button>
            <el-button @click="resetGroupQuery">
              <Icon icon="ep:refresh" class="mr-5px" /> 重置
            </el-button>
            <el-button
              type="danger"
              plain
              :disabled="isEmpty(groupCheckedIds)"
              @click="handleGroupDeleteBatch"
              v-hasPermi="['facebook:fb-collect-group:delete']"
            >
              <Icon icon="ep:delete" class="mr-5px" /> 批量删除
            </el-button>
            <el-button
              type="success"
              plain
              @click="handleGroupExport"
              v-hasPermi="['facebook:fb-collect-group:export']"
            >
              <Icon icon="ep:download" class="mr-5px" /> 导出
            </el-button>
          </el-form-item>
        </el-form>

        <!-- 群组列表 -->
        <el-table
          row-key="id"
          v-loading="groupLoading"
          :data="groupList"
          :stripe="true"
          :show-overflow-tooltip="true"
          @selection-change="handleGroupRowCheckboxChange"
        >
          <el-table-column type="selection" width="55" />
          <el-table-column label="ID" align="center" prop="id" width="80" />
          <el-table-column label="采集时间" align="center" prop="createTime" width="160">
            <template #default="scope">
              {{ formatDateTime(scope.row.createTime) }}
            </template>
          </el-table-column>
          <el-table-column label="分组" align="center" prop="resourceGroupId" width="110">
            <template #default="scope">{{ getResourceGroupName(scope.row.resourceGroupId) }}</template>
          </el-table-column>
          <el-table-column
            label="群组名称"
            align="center"
            prop="groupName"
            min-width="150"
            show-overflow-tooltip
          />
          <el-table-column
            label="群组链接"
            align="center"
            prop="url"
            min-width="250"
            show-overflow-tooltip
          >
            <template #default="scope">
              <el-link :href="scope.row.url" target="_blank" type="primary" v-if="scope.row.url">
                {{ scope.row.url }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="类型" align="center" prop="type" width="100" />
          <el-table-column label="成员数量" align="center" prop="memberQuantity" width="100" />
          <el-table-column label="活跃度" align="center" prop="activeQuantity" width="100" />
          <el-table-column label="加组次数" align="center" prop="joinGroupTimes" width="100" />
          <el-table-column label="评论次数" align="center" prop="commentTimes" width="100" />
          <el-table-column label="发帖次数" align="center" prop="postTimes" width="100" />
          <el-table-column
            label="备注"
            align="center"
            prop="remark"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column label="操作" align="center" width="120" fixed="right">
            <template #default="scope">
              <el-button
                link
                type="danger"
                @click="handleGroupDelete(scope.row.id)"
                v-hasPermi="['facebook:fb-collect-group:delete']"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页 -->
        <Pagination
          :total="groupTotal"
          v-model:page="groupQueryParams.pageNo"
          v-model:limit="groupQueryParams.pageSize"
          @pagination="getGroupList"
        />
      </el-tab-pane>

      <el-tab-pane label="帖子" name="post">
        <template #label>
          <span class="tab-label">
            <Icon icon="ep:document" class="mr-5px" />
            帖子
          </span>
        </template>

        <!-- 搜索栏 -->
        <el-form
          class="search-form mb-16px"
          :model="postQueryParams"
          ref="postQueryFormRef"
          :inline="true"
          label-width="80px"
        >
          <el-form-item label="发帖人" prop="postUser">
            <el-input
              v-model="postQueryParams.postUser"
              placeholder="请输入发帖人"
              clearable
              @keyup.enter="handlePostQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="群组名称" prop="groupName">
            <el-input
              v-model="postQueryParams.groupName"
              placeholder="请输入群组名称"
              clearable
              @keyup.enter="handlePostQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="分组">
            <ResourceGroupControl v-model="postQueryParams.resourceGroupId" resource-type="POST" title="帖子分组" @change="handlePostQuery" />
          </el-form-item>
          <el-form-item label="帖子内容" prop="postContent">
            <el-input
              v-model="postQueryParams.postContent"
              placeholder="请输入帖子内容关键词"
              clearable
              @keyup.enter="handlePostQuery"
              class="!w-200px"
            />
          </el-form-item>
          <el-form-item label="AI标签" prop="aiTags">
            <el-select v-model="postQueryParams.aiTags" placeholder="请选择AI标签" clearable class="!w-160px">
              <el-option v-for="tag in aiTagOptions" :key="tag" :label="tag" :value="tag" />
            </el-select>
          </el-form-item>
          <el-form-item label="意向等级" prop="intentLevel">
            <el-select v-model="postQueryParams.intentLevel" placeholder="请选择意向" clearable class="!w-140px">
              <el-option label="高" value="high" />
              <el-option label="中" value="medium" />
              <el-option label="低" value="low" />
              <el-option label="未知" value="unknown" />
            </el-select>
          </el-form-item>
          <el-form-item label="触达状态" prop="touchStatus">
            <el-select v-model="postQueryParams.touchStatus" placeholder="请选择状态" clearable class="!w-150px">
              <el-option label="未触达" value="not_touched" />
              <el-option label="已触达" value="touched" />
              <el-option label="已回复" value="replied" />
              <el-option label="已完成" value="done" />
            </el-select>
          </el-form-item>
          <el-form-item label="采集时间" prop="createTime">
            <el-date-picker
              v-model="postQueryParams.createTime"
              value-format="YYYY-MM-DD HH:mm:ss"
              type="daterange"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
              class="!w-240px"
            />
          </el-form-item>
          <el-form-item>
            <el-button @click="handlePostQuery">
              <Icon icon="ep:search" class="mr-5px" /> 搜索
            </el-button>
            <el-button @click="resetPostQuery">
              <Icon icon="ep:refresh" class="mr-5px" /> 重置
            </el-button>
            <el-button
              type="danger"
              plain
              :disabled="isEmpty(postCheckedIds)"
              @click="handlePostDeleteBatch"
              v-hasPermi="['facebook:fb-collect-post:delete']"
            >
              <Icon icon="ep:delete" class="mr-5px" /> 批量删除
            </el-button>
            <el-button
              type="success"
              plain
              @click="handlePostExport"
              v-hasPermi="['facebook:fb-collect-post:export']"
            >
              <Icon icon="ep:download" class="mr-5px" /> 导出
            </el-button>
            <el-button type="primary" plain @click="openPostImport">
              <Icon icon="ep:upload" class="mr-5px" /> 导入
            </el-button>
          </el-form-item>
        </el-form>

        <!-- 帖子列表 -->
        <el-table
          row-key="id"
          v-loading="postLoading"
          :data="postList"
          :stripe="true"
          :show-overflow-tooltip="true"
          @selection-change="handlePostRowCheckboxChange"
        >
          <el-table-column type="selection" width="55" />
          <el-table-column label="ID" align="center" prop="id" width="80" />
          <el-table-column label="采集时间" align="center" prop="createTime" width="160">
            <template #default="scope">
              {{ formatDateTime(scope.row.createTime) }}
            </template>
          </el-table-column>
          <el-table-column label="分组" align="center" prop="resourceGroupId" width="110">
            <template #default="scope">{{ getResourceGroupName(scope.row.resourceGroupId) }}</template>
          </el-table-column>
          <el-table-column
            label="发帖人"
            align="center"
            prop="postUser"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column
            label="帖子链接"
            align="center"
            prop="url"
            min-width="250"
            show-overflow-tooltip
          >
            <template #default="scope">
              <el-link :href="scope.row.url" target="_blank" type="primary" v-if="scope.row.url">
                {{ scope.row.url }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column
            label="帖子内容"
            align="center"
            prop="postContent"
            min-width="280"
            show-overflow-tooltip
          />
          <el-table-column
            label="群组名称"
            align="center"
            prop="groupName"
            min-width="120"
            show-overflow-tooltip
          />
          <el-table-column label="点赞数" align="center" prop="reactionCount" width="100" />
          <el-table-column label="评论数" align="center" prop="commentCount" width="100" />
          <el-table-column label="转发数" align="center" prop="reshareCount" width="100" />
          <el-table-column label="截流次数" align="center" prop="usedCount" width="100" />
          <el-table-column label="AI标签" align="center" prop="aiTags" width="180">
            <template #default="scope">
              <div class="tag-list" v-if="splitTags(scope.row.aiTags).length">
                <el-tag v-for="tag in splitTags(scope.row.aiTags)" :key="tag" size="small" class="mr-4px">
                  {{ tag }}
                </el-tag>
              </div>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="意向" align="center" prop="intentLevel" width="90">
            <template #default="scope">
              <el-tag :type="getIntentTagType(scope.row.intentLevel)">
                {{ getIntentLabel(scope.row.intentLevel) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="相关度" align="center" prop="productRelevanceScore" width="90">
            <template #default="scope">
              {{ scope.row.productRelevanceScore ?? '-' }}
            </template>
          </el-table-column>
          <el-table-column label="触达" align="center" prop="touchStatus" width="100">
            <template #default="scope">
              <el-tag :type="getTouchTagType(scope.row.touchStatus)">
                {{ getTouchLabel(scope.row.touchStatus) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="AI摘要" align="center" prop="aiSummary" width="220" show-overflow-tooltip />
          <el-table-column label="帖子创建时间" align="center" prop="postCreateTime" width="160">
            <template #default="scope">
              {{ formatDateTime(scope.row.postCreateTime) }}
            </template>
          </el-table-column>
          <el-table-column label="操作" align="center" width="120" fixed="right">
            <template #default="scope">
              <el-button
                link
                type="danger"
                @click="handlePostDelete(scope.row.id)"
                v-hasPermi="['facebook:fb-collect-post:delete']"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页 -->
        <Pagination
          :total="postTotal"
          v-model:page="postQueryParams.pageNo"
          v-model:limit="postQueryParams.pageSize"
          @pagination="getPostList"
        />
      </el-tab-pane>
    </el-tabs>
  </ContentWrap>

  <!-- 帖子导入弹窗 -->
  <PostImportForm ref="postImportFormRef" @success="getPostList" />
</template>

<script setup lang="ts" name="FacebookResource">
import { isEmpty } from '@/utils/is'
import { formatDate as formatDateUtil } from '@/utils/formatTime'
import download from '@/utils/download'
import { FbCollectUserApi, FbCollectUser } from '@/api/facebook/collectuser'
import { FbCollectGroupApi, FbCollectGroup } from '@/api/facebook/fbcollectgroup'
import { FbCollectPostApi, FbCollectPost } from '@/api/facebook/fbcollectpost'
import { FbResourceGroupApi, type FbResourceGroup } from '@/api/facebook/resourcegroup'
import { useMessage } from '@/hooks/web/useMessage'
import PostImportForm from './components/PostImportForm.vue'
import ResourceGroupControl from './components/ResourceGroupControl.vue'

const message = useMessage()
const resourceGroupNames = ref<Record<string, string>>({})
const loadResourceGroupNames = async () => {
  const all = await Promise.all([
    FbResourceGroupApi.getList('LEAD'),
    FbResourceGroupApi.getList('GROUP'),
    FbResourceGroupApi.getList('POST')
  ])
  const names: Record<string, string> = {}
  all.flat().forEach((item: FbResourceGroup) => { names[String(item.id)] = item.name })
  resourceGroupNames.value = names
}
const getResourceGroupName = (id?: number) => id ? resourceGroupNames.value[String(id)] || '未分组' : '未分组'

const aiTagOptions = [
  '高意向询价',
  '潜在经销商',
  '普通消费者',
  '竞品抱怨',
  '寻找供应商',
  '待人工确认',
  '已触达',
  '已完成'
]

// 当前激活的Tab
const activeTab = ref('user')

// 帖子导入表单引用
const postImportFormRef = ref()

// ==================== 用户相关 ====================
const userLoading = ref(true)
const userList = ref<FbCollectUser[]>([])
const userTotal = ref(0)
const userCheckedIds = ref<number[]>([])
const userQueryFormRef = ref()

const userQueryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  userName: undefined,
  fromResource: undefined,
  deepCollected: undefined,
  aiTags: undefined,
  intentLevel: undefined,
  touchStatus: undefined,
  resourceGroupId: undefined,
  createTime: []
})

/** 查询用户列表 */
const getUserList = async () => {
  userLoading.value = true
  try {
    const data = await FbCollectUserApi.getFbCollectUserPage(userQueryParams)
    userList.value = data.list
    userTotal.value = data.total
  } finally {
    userLoading.value = false
  }
}

/** 搜索用户 */
const handleUserQuery = () => {
  userQueryParams.pageNo = 1
  getUserList()
}

/** 重置用户搜索 */
const resetUserQuery = () => {
  userQueryFormRef.value?.resetFields()
  handleUserQuery()
}

/** 用户多选框变化 */
const handleUserRowCheckboxChange = (records: FbCollectUser[]) => {
  userCheckedIds.value = records.map((item) => item.id!)
}

/** 删除用户 */
const handleUserDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbCollectUserApi.deleteFbCollectUser(id)
    message.success('删除成功')
    await getUserList()
  } catch {}
}

/** 批量删除用户 */
const handleUserDeleteBatch = async () => {
  try {
    await message.delConfirm()
    await FbCollectUserApi.deleteFbCollectUserList(userCheckedIds.value)
    userCheckedIds.value = []
    message.success('删除成功')
    await getUserList()
  } catch {}
}

/** 导出用户 */
const handleUserExport = async () => {
  try {
    await message.exportConfirm()
    const data = await FbCollectUserApi.exportFbCollectUser(userQueryParams)
    download.excel(data, '潜客数据.xls')
  } catch {}
}

// ==================== 群组相关 ====================
const groupLoading = ref(true)
const groupList = ref<FbCollectGroup[]>([])
const groupTotal = ref(0)
const groupCheckedIds = ref<number[]>([])
const groupQueryFormRef = ref()

const groupQueryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  groupName: undefined,
  minMemberQuantity: undefined,
  maxMemberQuantity: undefined,
  resourceGroupId: undefined,
  createTime: []
})

/** 查询群组列表 */
const getGroupList = async () => {
  groupLoading.value = true
  try {
    const data = await FbCollectGroupApi.getFbCollectGroupPage(groupQueryParams)
    groupList.value = data.list
    groupTotal.value = data.total
  } finally {
    groupLoading.value = false
  }
}

/** 搜索群组 */
const handleGroupQuery = () => {
  groupQueryParams.pageNo = 1
  getGroupList()
}

/** 重置群组搜索 */
const resetGroupQuery = () => {
  groupQueryFormRef.value?.resetFields()
  handleGroupQuery()
}

/** 群组多选框变化 */
const handleGroupRowCheckboxChange = (records: FbCollectGroup[]) => {
  groupCheckedIds.value = records.map((item) => item.id!)
}

/** 删除群组 */
const handleGroupDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbCollectGroupApi.deleteFbCollectGroup(id)
    message.success('删除成功')
    await getGroupList()
  } catch {}
}

/** 批量删除群组 */
const handleGroupDeleteBatch = async () => {
  try {
    await message.delConfirm()
    await FbCollectGroupApi.deleteFbCollectGroupList(groupCheckedIds.value)
    groupCheckedIds.value = []
    message.success('删除成功')
    await getGroupList()
  } catch {}
}

/** 导出群组 */
const handleGroupExport = async () => {
  try {
    await message.exportConfirm()
    const data = await FbCollectGroupApi.exportFbCollectGroup(groupQueryParams)
    download.excel(data, '群组数据.xls')
  } catch {}
}

// ==================== 帖子相关 ====================
const postLoading = ref(true)
const postList = ref<FbCollectPost[]>([])
const postTotal = ref(0)
const postCheckedIds = ref<number[]>([])
const postQueryFormRef = ref()

const postQueryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  postUser: undefined,
  groupName: undefined,
  postContent: undefined,
  aiTags: undefined,
  intentLevel: undefined,
  touchStatus: undefined,
  resourceGroupId: undefined,
  createTime: []
})

/** 查询帖子列表 */
const getPostList = async () => {
  postLoading.value = true
  try {
    const data = await FbCollectPostApi.getFbCollectPostPage(postQueryParams)
    postList.value = data.list
    postTotal.value = data.total
  } finally {
    postLoading.value = false
  }
}

/** 搜索帖子 */
const handlePostQuery = () => {
  postQueryParams.pageNo = 1
  getPostList()
}

/** 重置帖子搜索 */
const resetPostQuery = () => {
  postQueryFormRef.value?.resetFields()
  handlePostQuery()
}

/** 帖子多选框变化 */
const handlePostRowCheckboxChange = (records: FbCollectPost[]) => {
  postCheckedIds.value = records.map((item) => item.id!)
}

/** 删除帖子 */
const handlePostDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbCollectPostApi.deleteFbCollectPost(id)
    message.success('删除成功')
    await getPostList()
  } catch {}
}

/** 批量删除帖子 */
const handlePostDeleteBatch = async () => {
  try {
    await message.delConfirm()
    await FbCollectPostApi.deleteFbCollectPostList(postCheckedIds.value)
    postCheckedIds.value = []
    message.success('删除成功')
    await getPostList()
  } catch {}
}

/** 导出帖子 */
const handlePostExport = async () => {
  try {
    await message.exportConfirm()
    const data = await FbCollectPostApi.exportFbCollectPost(postQueryParams)
    download.excel(data, '帖子数据.xls')
  } catch {}
}

/** 打开帖子导入弹窗 */
const openPostImport = () => {
  postImportFormRef.value?.open()
}

// ==================== 初始化 ====================
/** 格式化日期 */
const formatDate = (date: any) => {
  if (!date) return '-'
  return formatDateUtil(date)
}

/** 格式化日期时间 */
const formatDateTime = (date: any) => {
  if (!date) return '-'
  return formatDateUtil(date)
}

const splitTags = (tags?: string) => {
  if (!tags) return []
  return tags
    .split(/[,，]/)
    .map((tag) => tag.trim())
    .filter(Boolean)
}

const getIntentLabel = (level?: string) => {
  const map: Record<string, string> = {
    high: '高',
    medium: '中',
    low: '低',
    unknown: '未知'
  }
  return level ? map[level] || level : '-'
}

const getIntentTagType = (level?: string) => {
  const map: Record<string, 'success' | 'warning' | 'info' | 'danger'> = {
    high: 'danger',
    medium: 'warning',
    low: 'info',
    unknown: 'info'
  }
  return level ? map[level] || 'info' : 'info'
}

const getTouchLabel = (status?: string) => {
  const map: Record<string, string> = {
    not_touched: '未触达',
    touched: '已触达',
    replied: '已回复',
    done: '已完成'
  }
  return status ? map[status] || status : '未触达'
}

const getTouchTagType = (status?: string) => {
  const map: Record<string, 'success' | 'warning' | 'info' | 'danger'> = {
    not_touched: 'info',
    touched: 'warning',
    replied: 'success',
    done: 'success'
  }
  return status ? map[status] || 'info' : 'info'
}

/** 初始化 */
onMounted(() => {
  loadResourceGroupNames()
  getUserList()
})

/** Tab切换时加载对应数据 */
watch(activeTab, (newTab) => {
  if (newTab === 'user' && userList.value.length === 0) {
    getUserList()
  } else if (newTab === 'group' && groupList.value.length === 0) {
    getGroupList()
  } else if (newTab === 'post' && postList.value.length === 0) {
    getPostList()
  }
})
</script>

<style scoped lang="scss">
.tab-label {
  display: flex;
  align-items: center;
  font-size: 14px;
  font-weight: 500;
}

.search-form {
  :deep(.el-form-item) {
    margin-bottom: 12px;
  }
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 4px;
}
</style>
