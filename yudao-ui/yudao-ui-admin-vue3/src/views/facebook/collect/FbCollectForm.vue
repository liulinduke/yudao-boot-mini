<template>
  <Dialog
    :title="dialogTitle"
    v-model="dialogVisible"
    :width="formType === 'view' ? '90%' : '700px'"
  >
    <div v-loading="formLoading">
      <!-- 主表信息 -->
      <el-card class="mb-4" v-if="formType === 'view' && taskDetail">
        <template #header>
          <div class="card-header">
            <span>📋 任务基本信息</span>
          </div>
        </template>
        <el-descriptions :column="3" border>
          <el-descriptions-item label="任务ID">{{ taskDetail.id }}</el-descriptions-item>
          <el-descriptions-item label="任务类型">
            <dict-tag :type="DICT_TYPE.FB_COLLECT_TYPE" :value="taskDetail.taskType" />
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            <dict-tag :type="DICT_TYPE.SYS_COLLECT_STATUS" :value="taskDetail.status" />
          </el-descriptions-item>
          <el-descriptions-item label="总期望数量">{{
            taskDetail.totalExpectedCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="总已采集">{{
            taskDetail.totalCollectedCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="进度">
            <el-progress
              :percentage="getTotalProgress()"
              :status="taskDetail.status === 2 ? 'success' : undefined"
            />
          </el-descriptions-item>
          <el-descriptions-item label="账号数量">{{
            taskDetail.accountCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="链接数量">{{
            taskDetail.urlCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{
            formatDate(taskDetail.createTime)
          }}</el-descriptions-item>
          <el-descriptions-item label="开始时间">{{
            formatDate(taskDetail.startTime)
          }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{
            formatDate(taskDetail.endTime)
          }}</el-descriptions-item>
          <el-descriptions-item label="备注" :span="3">{{
            taskDetail.remark || '-'
          }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 新建/编辑表单 -->
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="140px"
        v-if="formType === 'create'"
      >
        <el-form-item label="采集账号" prop="accountIds">
          <el-select
            v-model="formData.accountIds"
            multiple
            placeholder="请选择账号"
            style="width: 100%"
            filterable
            v-show="accounts.length > 0"
          >
            <el-option
              v-for="account in accounts"
              :key="account.id"
              :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
              :value="account.id"
            />
          </el-select>
          <div v-if="accounts.length === 0" class="el-select-empty">
            暂无可用账号，请先添加Facebook账号
          </div>
        </el-form-item>

        <!-- 帖子采集：同一入口下按来源切换 -->
        <template v-if="formData.taskType === 2">
          <el-form-item label="采集来源">
            <el-radio-group v-model="postSourceMode" @change="handlePostSourceModeChange">
              <el-radio-button label="search">搜索结果</el-radio-button>
              <el-radio-button label="page">Page 帖子</el-radio-button>
              <el-radio-button label="group">群组帖子</el-radio-button>
            </el-radio-group>
          </el-form-item>

          <template v-if="postSourceMode === 'search'">
            <el-form-item label="搜索方式" prop="searchType">
              <el-select
                v-model="formData.searchType"
                placeholder="请选择搜索方式"
                style="width: 100%"
                @change="handleSearchTypeChange"
              >
                <el-option
                  v-for="dict in getIntDictOptions(DICT_TYPE.FB_SEARCH_TYPE)"
                  :key="dict.value"
                  :label="dict.label"
                  :value="dict.value"
                />
              </el-select>
            </el-form-item>

            <el-form-item v-if="formData.searchType === 1" label="关键词" prop="keyword">
              <el-input
                v-model="formData.keyword"
                type="textarea"
                :rows="4"
                placeholder="请输入搜索关键词，多个关键词请换行分隔"
                clearable
              />
            </el-form-item>

            <el-form-item v-if="formData.searchType === 0" label="搜索链接" prop="searchUrl">
              <el-input
                v-model="formData.searchUrl"
                type="textarea"
                :rows="4"
                placeholder="请输入 Facebook 搜索结果链接，多个链接请换行分隔。示例：https://www.facebook.com/search/top?q=关键词"
              />
            </el-form-item>
          </template>

          <template v-if="postSourceMode === 'page'">
            <el-form-item label="采集方式">
              <el-radio-group v-model="postPageInputMode" @change="handlePostPageInputModeChange">
                <el-radio label="manual">手动输入 Page 链接</el-radio>
                <el-radio label="select">从潜客选择</el-radio>
              </el-radio-group>
            </el-form-item>

            <el-form-item
              v-if="postPageInputMode === 'manual'"
              label="Page 链接"
              prop="searchUrl"
            >
              <el-input
                v-model="formData.searchUrl"
                type="textarea"
                :rows="4"
                placeholder="请输入 Page 主页链接，多个链接请换行分隔。示例：https://www.facebook.com/profile.php?id=xxx 或 https://www.facebook.com/pageName"
              />
            </el-form-item>

            <el-form-item v-if="postPageInputMode === 'select'" label="选择潜客">
              <div class="w-full">
                <el-button type="primary" @click="openPostPageSelector" class="mb-2">
                  <Icon icon="ep:plus" class="mr-5px" /> 选择潜客
                </el-button>
                <div v-if="selectedPostPages.length > 0" class="mt-2">
                  <el-tag
                    v-for="user in selectedPostPages"
                    :key="user.id"
                    closable
                    @close="removeSelectedPostPage(user.id)"
                    class="mr-2 mb-2"
                  >
                    {{ user.userName }}
                  </el-tag>
                </div>
                <div v-else class="text-gray-400 text-sm mt-2">
                  暂未选择潜客，请点击上方按钮选择
                </div>
              </div>
            </el-form-item>
          </template>

          <template v-if="postSourceMode === 'group'">
            <el-form-item label="采集方式">
              <el-radio-group v-model="postGroupInputMode" @change="handlePostGroupInputModeChange">
                <el-radio label="manual">手动输入群组链接</el-radio>
                <el-radio label="select">从群组选择</el-radio>
              </el-radio-group>
            </el-form-item>

            <el-form-item
              v-if="postGroupInputMode === 'manual'"
              label="群组链接"
              prop="searchUrl"
            >
              <el-input
                v-model="formData.searchUrl"
                type="textarea"
                :rows="4"
                placeholder="请输入群组主页链接，多个链接请换行分隔。示例：https://www.facebook.com/groups/xxx"
              />
            </el-form-item>

            <el-form-item v-if="postGroupInputMode === 'select'" label="选择群组">
              <div class="w-full">
                <el-button type="primary" @click="openPostGroupSelector" class="mb-2">
                  <Icon icon="ep:plus" class="mr-5px" /> 选择群组
                </el-button>
                <div v-if="selectedPostGroups.length > 0" class="mt-2">
                  <el-tag
                    v-for="group in selectedPostGroups"
                    :key="group.id"
                    closable
                    @close="removeSelectedPostGroup(group.id)"
                    class="mr-2 mb-2"
                  >
                    {{ group.groupName }}
                  </el-tag>
                </div>
                <div v-else class="text-gray-400 text-sm mt-2">
                  暂未选择群组，请点击上方按钮选择
                </div>
              </div>
            </el-form-item>
          </template>
        </template>

        <!-- 搜索方式和关键词（帖子采集、群成员采集、同行采集、帖子评论点赞采集不显示） -->
        <el-form-item
          v-if="
            formData.taskType !== 2 &&
            formData.taskType !== 7 &&
            formData.taskType !== 8 &&
            formData.taskType !== 11
          "
          label="搜索方式"
          prop="searchType"
        >
          <el-select
            v-model="formData.searchType"
            placeholder="请选择搜索方式"
            style="width: 100%"
            @change="handleSearchTypeChange"
          >
            <el-option
              v-for="dict in getIntDictOptions(DICT_TYPE.FB_SEARCH_TYPE)"
              :key="dict.value"
              :label="dict.label"
              :value="dict.value"
            />
          </el-select>
        </el-form-item>

        <!-- 关键词输入框(仅当searchType=1且taskType不是7、8或11时显示) -->
        <el-form-item
          v-if="
            formData.searchType === 1 &&
            formData.taskType !== 2 &&
            formData.taskType !== 7 &&
            formData.taskType !== 8 &&
            formData.taskType !== 11
          "
          label="关键词"
          prop="keyword"
        >
          <el-input
            v-model="formData.keyword"
            type="textarea"
            :rows="4"
            placeholder="请输入搜索关键词，多个关键词请换行分隔"
            clearable
          />
        </el-form-item>

        <!-- 采集链接输入框(仅当searchType=0且taskType不是7、8或11时显示) -->
        <el-form-item
          v-if="
            formData.searchType === 0 &&
            formData.taskType !== 2 &&
            formData.taskType !== 7 &&
            formData.taskType !== 8 &&
            formData.taskType !== 11
          "
          label="采集链接"
          prop="searchUrl"
        >
          <el-input
            v-model="formData.searchUrl"
            type="textarea"
            :rows="4"
            :placeholder="getUrlPlaceholder(formData.taskType)"
          />
        </el-form-item>

        <!-- 帖子评论点赞采集：直接输入帖子链接 -->
        <el-form-item v-if="formData.taskType === 11" label="帖子链接" prop="searchUrl">
          <el-input
            v-model="formData.searchUrl"
            type="textarea"
            :rows="4"
            :placeholder="getUrlPlaceholder(formData.taskType)"
          />
        </el-form-item>

        <!-- 帖子评论点赞采集：采集选项 -->
        <el-form-item v-if="formData.taskType === 11" label="采集选项">
          <el-checkbox-group v-model="commentLikeOptions">
            <el-checkbox label="comment">采集评论</el-checkbox>
            <el-checkbox label="like">采集点赞数</el-checkbox>
          </el-checkbox-group>
        </el-form-item>

        <!-- 帖子评论点赞采集：评论期望数量（必填） -->
        <el-form-item
          v-if="formData.taskType === 11 && commentLikeOptions.includes('comment')"
          label="评论期望数量"
          prop="commentExpectedCount"
        >
          <el-input-number
            v-model="formData.commentExpectedCount"
            :min="1"
            :max="10000"
            style="width: 100%"
          />
        </el-form-item>

        <el-form-item
          v-if="formData.taskType === 11 && commentLikeOptions.includes('like')"
          label="点赞期望数量"
          prop="likeExpectedCount"
        >
          <el-input-number
            v-model="formData.likeExpectedCount"
            :min="1"
            :max="10000"
            style="width: 100%"
          />
        </el-form-item>

        <!-- 群组成员采集特殊UI：链接输入和群组选择二选一 -->
        <el-form-item v-if="formData.taskType === 7" label="采集方式" prop="searchUrl">
          <el-radio-group v-model="groupInputMode" @change="handleGroupInputModeChange">
            <el-radio label="manual">手动输入链接</el-radio>
            <el-radio label="select">从群组选择</el-radio>
          </el-radio-group>
        </el-form-item>

        <!-- 手动输入链接模式 -->
        <el-form-item
          v-if="formData.taskType === 7 && groupInputMode === 'manual'"
          label="采集链接"
          prop="searchUrl"
        >
          <el-input
            v-model="formData.searchUrl"
            type="textarea"
            :rows="4"
            :placeholder="getUrlPlaceholder(formData.taskType)"
          />
        </el-form-item>

        <!-- 从群组选择模式 -->
        <el-form-item
          v-if="formData.taskType === 7 && groupInputMode === 'select'"
          label="选择群组"
        >
          <div class="w-full">
            <el-button type="primary" @click="openGroupSelector" class="mb-2">
              <Icon icon="ep:plus" class="mr-5px" /> 选择群组
            </el-button>
            <div v-if="selectedGroups.length > 0" class="mt-2">
              <el-tag
                v-for="group in selectedGroups"
                :key="group.id"
                closable
                @close="removeSelectedGroup(group.id)"
                class="mr-2 mb-2"
              >
                {{ group.groupName }}
              </el-tag>
            </div>
            <div v-else class="text-gray-400 text-sm mt-2"> 暂未选择群组，请点击上方按钮选择 </div>
          </div>
        </el-form-item>

        <!-- 同行采集特殊UI：关系类型选择 -->
        <el-form-item v-if="formData.taskType === 8" label="关系类型">
          <el-checkbox-group v-model="relationTypes">
            <el-checkbox label="followers">粉丝</el-checkbox>
            <el-checkbox label="following">关注</el-checkbox>
            <el-checkbox label="friends">好友</el-checkbox>
          </el-checkbox-group>
        </el-form-item>

        <!-- 同行采集：链接输入和用户选择二选一 -->
        <el-form-item v-if="formData.taskType === 8" label="采集方式">
          <el-radio-group v-model="userInputMode" @change="handleUserInputModeChange">
            <el-radio label="manual">手动输入链接</el-radio>
            <el-radio label="select">从潜客选择</el-radio>
          </el-radio-group>
        </el-form-item>

        <!-- 手动输入链接模式 -->
        <el-form-item
          v-if="formData.taskType === 8 && userInputMode === 'manual'"
          label="采集链接"
          prop="searchUrl"
        >
          <el-input
            v-model="formData.searchUrl"
            type="textarea"
            :rows="4"
            placeholder="请输入用户主页链接，多个链接请换行分隔&#10;例如：https://www.facebook.com/profile.php?id=xxx"
          />
        </el-form-item>

        <!-- 从潜客选择模式 -->
        <el-form-item v-if="formData.taskType === 8 && userInputMode === 'select'" label="选择用户">
          <div class="w-full">
            <el-button type="primary" @click="openUserSelector" class="mb-2">
              <Icon icon="ep:plus" class="mr-5px" /> 选择用户
            </el-button>
            <div v-if="selectedUsers.length > 0" class="mt-2">
              <el-tag
                v-for="user in selectedUsers"
                :key="user.id"
                closable
                @close="removeSelectedUser(user.id)"
                class="mr-2 mb-2"
              >
                {{ user.userName }}
              </el-tag>
            </div>
            <div v-else class="text-gray-400 text-sm mt-2"> 暂未选择用户，请点击上方按钮选择 </div>
          </div>
        </el-form-item>

        <el-form-item v-if="formData.taskType !== 11" label="期望数量" prop="expectedCount">
          <el-input-number
            v-model="formData.expectedCount"
            :min="1"
            :max="10000"
            style="width: 100%"
          />
        </el-form-item>

        <el-form-item label="采集间隔(秒)" prop="intervalSeconds">
          <el-input-number
            v-model="formData.intervalSeconds"
            :min="1"
            :max="300"
            style="width: 100%"
          />
        </el-form-item>

        <el-form-item label="备注" prop="remark">
          <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
        </el-form-item>
      </el-form>

      <!-- 编辑模式下的Tab -->
      <el-tabs v-if="formType === 'view'" v-model="activeTab" type="border-card">
        <!-- Tab 1: 采集明细 -->
        <el-tab-pane label="📊 采集明细" name="details">
          <el-table :data="detailList" stripe border max-height="500">
            <el-table-column label="FB账号" prop="fbAccount" width="150" />
            <el-table-column
              label="采集链接"
              prop="searchUrl"
              min-width="300"
              show-overflow-tooltip
            />
            <el-table-column label="期望/已采" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.collectedCount || 0 }}
              </template>
            </el-table-column>
            <el-table-column label="进度" width="150">
              <template #default="scope">
                <el-progress
                  :percentage="getDetailProgress(scope.row)"
                  :status="scope.row.status === 2 ? 'success' : undefined"
                  :stroke-width="12"
                />
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100">
              <template #default="scope">
                <dict-tag :type="DICT_TYPE.SYS_COLLECT_STATUS" :value="scope.row.status" />
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <!-- Tab 2: 采集结果 -->
        <el-tab-pane label="👥 采集结果" name="results">
          <!-- 帖子采集结果显示 -->
          <el-table
            v-if="taskDetail?.taskType === 2"
            :data="filteredUserList"
            stripe
            border
            max-height="500"
          >
            <el-table-column label="发帖人" prop="postUser" width="150" />
            <el-table-column label="帖子链接" prop="url" min-width="250" show-overflow-tooltip />
            <el-table-column label="来源" prop="fromResource" width="100" />
            <el-table-column label="群组名称" prop="groupName" width="150" show-overflow-tooltip />
            <el-table-column label="点赞数" prop="reactionCount" width="100" />
            <el-table-column label="评论数" prop="commentCount" width="100" />
            <el-table-column label="转发数" prop="reshareCount" width="100" />
            <el-table-column
              label="帖子内容"
              prop="postContent"
              min-width="300"
              show-overflow-tooltip
            />
          </el-table>

          <!-- 帖子评论点赞采集结果显示 -->
          <el-table
            v-else-if="taskDetail?.taskType === 11"
            :data="filteredUserList"
            stripe
            border
            max-height="500"
          >
            <el-table-column label="评论者" prop="userName" width="150" />
            <el-table-column label="主页链接" prop="url" min-width="250" show-overflow-tooltip />
            <el-table-column label="头像" width="80">
              <template #default="scope">
                <el-image
                  v-if="scope.row.avatar"
                  :src="scope.row.avatar"
                  fit="cover"
                  style="width: 40px; height: 40px; border-radius: 50%"
                  preview-teleported
                />
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="点赞数" prop="followers" width="100" />
            <el-table-column label="回复数" width="100">
              <template #default="scope">
                {{ scope.row.config ? JSON.parse(scope.row.config).replyCount || 0 : 0 }}
              </template>
            </el-table-column>
            <el-table-column label="评论时间" prop="lastPostSummary" width="120" />
            <el-table-column
              label="评论内容"
              prop="profileStatus"
              min-width="300"
              show-overflow-tooltip
            />
            <el-table-column label="来源" prop="fromResource" width="120" />
          </el-table>

          <!-- 群组采集结果显示 -->
          <el-table
            v-else-if="taskDetail?.taskType === 4"
            :data="filteredUserList"
            stripe
            border
            max-height="500"
          >
            <el-table-column label="群组名称" prop="groupName" width="200" />
            <el-table-column label="群组链接" prop="url" min-width="300" show-overflow-tooltip />
            <el-table-column label="类型" prop="type" width="100" />
            <el-table-column label="成员数" prop="memberQuantity" width="120" />
            <el-table-column label="活跃度" prop="activeQuantity" width="150" />
          </el-table>

          <!-- 用户采集结果显示（默认） -->
          <el-table v-else :data="filteredUserList" stripe border max-height="500">
            <el-table-column label="Facebook ID" prop="fbUserId" width="180" />
            <el-table-column label="用户名" prop="userName" width="150" />
            <el-table-column label="主页链接" prop="url" min-width="250" show-overflow-tooltip />
            <el-table-column label="粉丝数" prop="followers" width="100" />
            <el-table-column label="所在地" prop="city" width="120" />
            <el-table-column label="邮箱" prop="email" width="180" />
            <el-table-column label="电话" prop="phonenumber" width="130" />
          </el-table>

          <el-pagination
            v-if="userList.length > 0"
            class="mt-4"
            v-model:current-page="userPageNo"
            v-model:page-size="userPageSize"
            :total="userList.length"
            :page-sizes="[10, 20, 50, 100]"
            layout="total, sizes, prev, pager, next, jumper"
            @size-change="handleUserSizeChange"
            @current-change="handleUserPageChange"
          />
          <el-empty v-if="userList.length === 0" description="暂无采集数据" />
        </el-tab-pane>
      </el-tabs>
    </div>

    <template #footer>
      <el-button
        type="primary"
        @click="submitForm"
        :loading="formLoading"
        v-if="formType === 'create'"
      >
        确 定
      </el-button>
      <el-button @click="dialogVisible = false">关 闭</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器组件 -->
  <GroupSelector v-model="groupSelectorVisible" @confirm="handleGroupConfirm" />

  <!-- 用户选择器组件 -->
  <UserSelector v-model="userSelectorVisible" @confirm="handleUserConfirm" />
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbCollectApi, FbCollect } from '@/api/facebook/collect'
import { FbAccountApi } from '@/api/facebook/account'
import { FbCollectUserApi, FbCollectUser } from '@/api/facebook/collectuser'
import { FbCollectGroupApi, FbCollectGroup } from '@/api/facebook/fbcollectgroup'
import { FbCollectPostApi, FbCollectPost } from '@/api/facebook/fbcollectpost'
import { startBrowserCollect } from '@/utils/wpfBridge'
import { DICT_TYPE, getIntDictOptions } from '@/utils/dict'
import { useI18n } from '@/hooks/web/useI18n'
import { useMessage } from '@/hooks/web/useMessage'
import request from '@/config/axios'
import GroupSelector from './components/GroupSelector.vue'
import UserSelector from './components/UserSelector.vue'

const { t } = useI18n() // 国际化
const message = useMessage() // 消息弹窗

const dialogVisible = ref(false) // 弹窗的是否展示
const dialogTitle = ref('') // 弹窗的标题
const formLoading = ref(false) // 表单的加载中：1）修改时的数据加载；2）提交的按钮禁用
const formType = ref('') // 表单的类型：create - 新增；update - 修改
const activeTab = ref('details') // 当前激活的Tab
const taskDetail = ref<any>(null) // 主表详情
const detailList = ref<any[]>([]) // 明细列表
const userList = ref<any[]>([]) // 采集结果列表
const userPageNo = ref(1) // 用户数据页码
const userPageSize = ref(20) // 用户数据每页条数
const filteredUserList = computed(() => {
  // 分页
  const start = (userPageNo.value - 1) * userPageSize.value
  const end = start + userPageSize.value
  return userList.value.slice(start, end)
})

const formData = ref({
  id: undefined,
  accountIds: [] as string[],
  taskType: undefined, // 采集类型(1主页/2帖子/3用户等),由功能卡片自动设置
  searchType: 1, // 搜索方式(0链接/1关键词),默认关键词采集
  keyword: '', // 搜索关键词(仅searchType=1时使用)
  searchUrl: '',
  expectedCount: 100,
  commentExpectedCount: 100, // 评论期望数量
  likeExpectedCount: 100, // 点赞期望数量
  intervalSeconds: 5,
  remark: ''
})
const formRules = reactive({
  accountIds: [{ required: true, message: '请选择采集账号', trigger: 'change' }],
  taskType: [{ required: true, message: '请选择采集类型', trigger: 'change' }],
  searchType: [
    {
      required: true,
      message: '请选择搜索方式',
      trigger: 'change',
      validator: (rule: any, value: any, callback: any) => {
        // 群成员采集、同行采集、帖子评论点赞采集不需要验证搜索方式
        if (
          (formData.value.taskType === 2 && postSourceMode.value !== 'search') ||
          formData.value.taskType === 7 ||
          formData.value.taskType === 8 ||
          formData.value.taskType === 11
        ) {
          callback()
        } else if (value === undefined || value === null) {
          callback(new Error('请选择搜索方式'))
        } else {
          callback()
        }
      }
    }
  ],
  keyword: [
    {
      required: true,
      message: '请输入搜索关键词',
      trigger: 'blur',
      validator: (rule: any, value: any, callback: any) => {
        // 群成员采集、同行采集、帖子评论点赞采集不需要验证关键词
        if (
          (formData.value.taskType === 2 && postSourceMode.value !== 'search') ||
          formData.value.taskType === 7 ||
          formData.value.taskType === 8 ||
          formData.value.taskType === 11
        ) {
          callback()
        } else if (formData.value.searchType === 1 && !value) {
          callback(new Error('请输入搜索关键词'))
        } else {
          callback()
        }
      }
    }
  ],
  searchUrl: [
    {
      required: true,
      message: '请输入采集链接',
      trigger: 'blur',
      validator: (rule: any, value: any, callback: any) => {
        // 群成员采集、同行采集、帖子评论点赞采集有自己的链接输入方式，不在此验证
        if (
          (formData.value.taskType === 2 &&
            postSourceMode.value === 'search' &&
            formData.value.searchType === 1) ||
          formData.value.taskType === 7 ||
          formData.value.taskType === 8 ||
          formData.value.taskType === 11
        ) {
          callback()
        } else if (formData.value.searchType === 0 && !value) {
          callback(new Error('请输入采集链接'))
        } else {
          callback()
        }
      }
    }
  ],
  expectedCount: [{ required: true, message: '请输入期望数量', trigger: 'blur' }],
  commentExpectedCount: [{ required: true, message: '请输入评论期望数量', trigger: 'blur' }],
  likeExpectedCount: [{ required: true, message: '请输入点赞期望数量', trigger: 'blur' }],
  intervalSeconds: [{ required: true, message: '请输入采集间隔', trigger: 'blur' }]
})
const formRef = ref() // 表单 Ref
const accounts = ref<any[]>([]) // 账号列表

// 群组选择相关
const groupSelectorVisible = ref(false) // 群组选择弹框显示状态
const selectedGroups = ref<FbCollectGroup[]>([]) // 已选择的群组
const groupInputMode = ref<'manual' | 'select'>('select') // 群组成员采集输入模式：manual-手动输入, select-从群组选择

// 同行采集相关
const relationTypes = ref<string[]>(['followers']) // 默认选中粉丝
const userInputMode = ref<'manual' | 'select'>('select') // 同行采集输入模式
const userSelectorVisible = ref(false) // 用户选择弹框显示状态
const selectedUsers = ref<FbCollectUser[]>([]) // 已选择的用户

// 帖子采集相关
const postSourceMode = ref<'search' | 'page' | 'group'>('search')
const postPageInputMode = ref<'manual' | 'select'>('select')
const postGroupInputMode = ref<'manual' | 'select'>('select')
const selectedPostPages = ref<FbCollectUser[]>([])
const selectedPostGroups = ref<FbCollectGroup[]>([])

// 帖子评论点赞采集相关
const commentLikeOptions = ref<string[]>(['comment', 'like']) // 默认同时采集评论和点赞
const likeExpectedCount = ref(100) // 点赞期望数量

/** 打开弹窗 */
const open = async (type: string, id?: number, taskTypeValue?: number) => {
  dialogVisible.value = true
  // 设置标题
  if (type === 'view') {
    dialogTitle.value = '任务详情'
  } else if (type === 'create') {
    dialogTitle.value = '新建任务'
  } else {
    dialogTitle.value = t('action.' + type)
  }
  formType.value = type
  resetForm()

  // 如果是新建模式且传入了taskType,则设置它
  if (type === 'create' && taskTypeValue !== undefined) {
    formData.value.taskType = taskTypeValue
  }

  // 加载账号列表
  await loadAccounts()

  // 修改时，设置数据
  if (id) {
    formLoading.value = true
    try {
      // 加载主表信息
      const data = await FbCollectApi.getFbCollect(id)
      taskDetail.value = data
      formData.value = data

      // 加载明细列表
      await loadDetailList(id)

      // 加载采集结果
      await loadUserList(id)
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open }) // 提供 open 方法，用于打开弹窗

/** 加载明细列表 */
const loadDetailList = async (taskId: number | string) => {
  try {
    console.log('🔍 加载明细列表, taskId:', taskId, 'type:', typeof taskId)
    const response = await request.get({
      url: '/facebook/fb-collect-detail/list-by-task',
      params: { taskId } // 直接传递,让qs库处理
    })
    console.log('📦 API响应:', response)
    console.log('📦 响应类型:', Array.isArray(response) ? 'Array' : typeof response)

    // response 已经是数据本身(拦截器已处理)
    let dataList = []
    if (Array.isArray(response)) {
      dataList = response
    } else if (response && response.data && Array.isArray(response.data)) {
      dataList = response.data
    } else if (response && response.list && Array.isArray(response.list)) {
      dataList = response.list
    }

    console.log('✅ 明细列表加载成功, 数量:', dataList.length)
    console.log('📋 第一条数据:', dataList[0])
    detailList.value = dataList
  } catch (error) {
    console.error('❌ 加载明细列表失败:', error)
    detailList.value = []
  }
}

/** 加载采集结果 */
const loadUserList = async (taskId: number) => {
  try {
    // 根据任务类型加载不同的采集结果
    const taskType = taskDetail.value?.taskType
    const allResults: any[] = []
    let pageNo = 1
    const pageSize = 200

    while (true) {
      let response: any

      // 根据任务类型调用不同的API
      if (taskType === 2) {
        // 帖子采集 - 查询 fb_collect_post 表
        response = await FbCollectPostApi.getFbCollectPostPage({
          pageNo,
          pageSize,
          taskId
        })
      } else if (taskType === 4) {
        // 群组采集 - 查询 fb_collect_group 表
        response = await FbCollectGroupApi.getFbCollectGroupPage({
          pageNo,
          pageSize,
          taskId
        })
      } else {
        // 用户采集、帖子评论点赞采集等其他类型 - 查询 fb_collect_user 表
        response = await FbCollectUserApi.getFbCollectUserPage({
          pageNo,
          pageSize,
          taskId
        })
      }

      const list = response.list || []
      allResults.push(...list)

      // 如果返回的数据少于pageSize,说明已经是最后一页
      if (list.length < pageSize) {
        break
      }

      pageNo++
    }

    userList.value = allResults
    userPageNo.value = 1 // 重置页码
  } catch (error) {
    console.error('加载采集结果失败:', error)
    userList.value = []
  }
}

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    console.log('=== 加载账号列表 ===')
    console.log('API返回数据:', data)
    console.log('data.list:', data.list)

    if (data && data.list && Array.isArray(data.list)) {
      accounts.value = data.list
      console.log('赋值后的accounts.value:', accounts.value)
      console.log('第一个账号对象:', accounts.value[0])
      console.log('第一个账号的id:', accounts.value[0]?.id)
      console.log('第一个账号的fbAccount:', accounts.value[0]?.fbAccount)
      console.log('账号列表加载成功:', accounts.value.length, '个账号')
    } else {
      console.error('账号列表数据格式错误:', data)
      accounts.value = []
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accounts.value = []
  }
}

/** 提交表单 */
const emit = defineEmits(['success']) // 定义 success 事件,用于操作成功后的回调
const submitForm = async () => {
  // 校验表单
  if (!formRef.value) return
  await formRef.value.validate()
  // 提交请求
  formLoading.value = true
  try {
    const data = formData.value as unknown as FbCollect

    // 解析URL列表(支持换行分隔)
    if (data.taskType === 2) {
      data.searchUrl = normalizePostCollectUrls(data.searchUrl || '')
    }
    let urls = (data.searchUrl || '').split('\n').filter((url) => url.trim())

    // ✅ 群组成员采集(taskType=7):自动为群组链接添加/members后缀
    if (data.taskType === 7 && urls.length > 0) {
      urls = urls.map((url) => {
        const trimmedUrl = url.trim()
        // 如果URL是群组链接但不包含/members,则添加
        if (trimmedUrl.includes('/groups/') && !trimmedUrl.includes('/members')) {
          return `${trimmedUrl}/members`
        }
        return trimmedUrl
      })
      console.log('✅ 群组成员采集URL已自动补全:', urls)
    }

    // 获取选中的账号信息
    const selectedAccounts = accounts.value.filter((acc) =>
      (data as any).accountIds?.includes(acc.id)
    )

    // 准备任务数据(包含所有账号和URL)
    const taskData: any = {
      accountIds: selectedAccounts.map((acc) => acc.id), // 传递账号ID列表
      fbAccount: selectedAccounts[0]?.fbAccount, // 使用第一个账号的 fbAccount(后端会遍历)
      taskType: data.taskType, // 采集类型(1主页/2帖子/3用户等)
      searchType: data.searchType, // 搜索方式(0链接/1关键词)
      searchUrl: urls.join('\n'), // 将所有URL用换行符连接
      expectedCount: data.expectedCount,
      intervalSeconds: data.intervalSeconds,
      status: 0, // 待执行
      remark: data.remark
    }

    // 如果是帖子评论点赞采集，将配置保存到remark字段（临时方案）
    if (data.taskType === 11) {
      const config = {
        collectComment: commentLikeOptions.value.includes('comment'),
        collectLike: commentLikeOptions.value.includes('like'),
        commentExpectedCount: formData.value.commentExpectedCount || 100,
        likeExpectedCount: formData.value.likeExpectedCount || 100
      }
      // 将配置JSON附加到remark后面
      taskData.remark = `${data.remark || ''}
__CONFIG__:${JSON.stringify(config)}`
    }

    // 一次性创建任务和所有明细
    let details: Array<{ detailId: number; fbAccount: string; searchUrl: string }> = []
    if (formType.value === 'create') {
      const result = await FbCollectApi.createFbCollect(taskData)
      // 后端返回 FbCollectCreateRespVO，包含 taskId 和 details 列表
      const respData = result.data || result
      details = respData.details || []
      console.log('✅ 任务创建成功, 主表ID:', respData.taskId, ', 明细数量:', details.length)
    } else {
      taskData.id = data.id
      await FbCollectApi.updateFbCollect(taskData)
      // 编辑模式暂不支持批量启动浏览器
      message.success(t('common.updateSuccess'))
      dialogVisible.value = false
      emit('success')
      return
    }

    // 为每个账号启动第一个浏览器的采集(后续任务由后端调度复用)
    const createdTasks: Array<{ detailId: number; fbAccount: string; url: string }> = []

    // 按账号分组,每个账号只启动第一个任务
    const accountFirstDetailMap = new Map<string, (typeof details)[0]>()
    for (const detail of details) {
      if (!accountFirstDetailMap.has(detail.fbAccount)) {
        accountFirstDetailMap.set(detail.fbAccount, detail)
      }
    }

    // 为每个账号启动第一个浏览器的采集
    for (const [fbAccount, firstDetail] of accountFirstDetailMap.entries()) {
      try {
        // 从选中的账号列表中获取该账号的 Cookie
        const selectedAccount = selectedAccounts.find((acc) => acc.fbAccount === fbAccount)
        const cookie = selectedAccount?.cookie || null

        console.log(`🔍 账号 ${fbAccount} 的 Cookie:`, cookie ? '已提供' : '未提供')

        // 构建配置JSON（仅针对帖子评论点赞采集）
        let configJson: string | undefined = undefined
        if (data.taskType === 11) {
          configJson = JSON.stringify({
            collectComment: commentLikeOptions.value.includes('comment'),
            collectLike: commentLikeOptions.value.includes('like'),
            commentExpectedCount: formData.value.commentExpectedCount || 100,
            likeExpectedCount: formData.value.likeExpectedCount || 100
          })
          console.log('📋 帖子评论点赞采集配置:', configJson)
        }

        startBrowserCollect(
          String(firstDetail.detailId),
          String(fbAccount),
          cookie, // ✅ 传入 Cookie
          firstDetail.searchUrl,
          data.expectedCount,
          data.taskType, // 传递任务类型(1主页/2帖子/3用户等)
          configJson // 传递配置（可选）
        )
        createdTasks.push({
          detailId: firstDetail.detailId,
          fbAccount: fbAccount,
          url: firstDetail.searchUrl
        })
        console.log(
          `🚀 启动采集: 明细ID=${firstDetail.detailId}, 账号=${fbAccount}, 类型=${data.taskType}`
        )
      } catch (error) {
        console.error(`启动账号 ${fbAccount} 的浏览器失败:`, error)
        message.warning(`账号 ${fbAccount} 启动浏览器失败`)
      }
    }

    if (createdTasks.length > 0) {
      message.success(`已创建 ${createdTasks.length} 个任务并启动采集`)
      console.log('已启动的任务:', createdTasks)
    } else {
      message.success(
        t('common.' + (formType.value === 'create' ? 'createSuccess' : 'updateSuccess'))
      )
    }
    dialogVisible.value = false
    // 发送操作成功的事件
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    id: undefined,
    accountIds: [],
    taskType: undefined, // 采集类型(由功能卡片自动设置)
    searchType: 1, // 搜索方式(默认关键词采集)
    keyword: '',
    searchUrl: '',
    expectedCount: 100,
    commentExpectedCount: 100,
    likeExpectedCount: 100,
    intervalSeconds: 5,
    remark: ''
  }
  taskDetail.value = null
  detailList.value = []
  userList.value = []
  userPageNo.value = 1
  userPageSize.value = 20
  activeTab.value = 'details'
  commentLikeOptions.value = ['comment', 'like'] // 重置评论点赞选项
  likeExpectedCount.value = 100 // 重置点赞期望数量
  postSourceMode.value = 'search'
  postPageInputMode.value = 'select'
  postGroupInputMode.value = 'select'
  selectedPostPages.value = []
  selectedPostGroups.value = []
  selectedGroups.value = []
  selectedUsers.value = []
  groupInputMode.value = 'select'
  userInputMode.value = 'select'
  formRef.value?.resetFields()
}

/** 计算总进度 */
const getTotalProgress = () => {
  if (!taskDetail.value || !taskDetail.value.totalExpectedCount) return 0
  return Math.min(
    100,
    Math.round((taskDetail.value.totalCollectedCount / taskDetail.value.totalExpectedCount) * 100)
  )
}

/** 计算明细进度 */
const getDetailProgress = (detail: any) => {
  if (!detail.expectedCount) return 0
  return Math.min(100, Math.round(((detail.collectedCount || 0) / detail.expectedCount) * 100))
}

/** 格式化日期 */
const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-'
  const d = new Date(date)
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

/** 用户数据页码变化 */
const handleUserPageChange = (page: number) => {
  userPageNo.value = page
}

/** 用户数据每页条数变化 */
const handleUserSizeChange = (size: number) => {
  userPageSize.value = size
  userPageNo.value = 1 // 重置到第一页
}

/** 根据任务类型获取URL提示文本 */
const getUrlPlaceholder = (taskType?: number) => {
  const placeholders: Record<number, string> = {
    1: '请输入主页采集链接，多个链接请换行分隔。\n示例：https://facebook.com/search/pages?q=关键词',
    2: '请输入帖子采集链接，多个链接请换行分隔。\n示例：https://facebook.com/search/top?q=关键词',
    3: '请输入用户采集链接，多个链接请换行分隔。\n在 Facebook 搜索目标用户后，复制浏览器地址栏的完整链接。\n示例：https://facebook.com/search/people?q=关键词',
    4: '请输入群组采集链接，多个链接请换行分隔。\n示例：https://facebook.com/groups/xxx',
    5: '请输入活动采集链接，多个链接请换行分隔。\n示例：https://facebook.com/events/xxx',
    6: '请输入评论采集链接，多个链接请换行分隔。\n示例：https://facebook.com/xxx/posts/xxx',
    7: '请输入群组成员采集链接，多个链接请换行分隔。\n可以通过“选择群组”按钮从已有群组中选择，或手动输入。\n示例：https://facebook.com/groups/xxx/members',
    11: '请输入帖子链接，多个链接请换行分隔。\n将采集该帖子下的所有评论和点赞数。\n示例：https://facebook.com/xxx/posts/xxx'
  }
  return placeholders[taskType || 1] || '请输入采集链接，多个链接请换行分隔'
}

/** 根据taskType和关键词生成搜索URL */
const generateSearchUrl = (taskType: number, keyword: string): string => {
  if (!keyword) return ''

  // 根据不同的taskType生成不同的搜索URL
  const urlMap: Record<number, string> = {
    1: `https://www.facebook.com/search/pages?q=${encodeURIComponent(keyword)}`, // 主页采集
    2: `https://www.facebook.com/search/top?q=${encodeURIComponent(keyword)}`, // 帖子采集
    3: `https://www.facebook.com/search/people?q=${encodeURIComponent(keyword)}`, // 用户采集
    4: `https://www.facebook.com/search/groups?q=${encodeURIComponent(keyword)}`, // 群组采集
    5: `https://www.facebook.com/search/events?q=${encodeURIComponent(keyword)}`, // 活动采集
    6: `https://www.facebook.com/search/top?q=${encodeURIComponent(keyword)}`, // 评论采集(先搜索帖子)
    11: `https://www.facebook.com/search/top?q=${encodeURIComponent(keyword)}` // 帖子评论点赞采集(先搜索帖子)
  }

  return urlMap[taskType] || urlMap[1]
}

const splitInputUrls = (value: string) =>
  (value || '')
    .split('\n')
    .map((url) => url.trim())
    .filter(Boolean)

const cleanFacebookUrl = (url: string): string => {
  try {
    const parsed = new URL(url.trim())
    parsed.hash = ''
    return parsed.toString()
  } catch {
    return url.trim()
  }
}

const toPagePostsUrl = (url: string): string => {
  try {
    const parsed = new URL(cleanFacebookUrl(url))
    parsed.hash = ''
    return parsed.toString()
  } catch {
    return url.trim()
  }
}

const toGroupPostsUrl = (url: string): string => {
  try {
    const parsed = new URL(cleanFacebookUrl(url))
    const match = parsed.pathname.match(/\/groups\/([^/]+)/)
    if (match?.[1]) {
      return `${parsed.origin}/groups/${match[1]}`
    }
    parsed.search = ''
    parsed.hash = ''
    return parsed.toString().replace(/\/+$/, '')
  } catch {
    return url.trim()
  }
}

const normalizePostCollectUrls = (value: string): string => {
  const urls = splitInputUrls(value)
  if (postSourceMode.value === 'page') {
    return urls.map(toPagePostsUrl).join('\n')
  }
  if (postSourceMode.value === 'group') {
    return urls.map(toGroupPostsUrl).join('\n')
  }
  return urls.map(cleanFacebookUrl).join('\n')
}

/** 根据多个关键词生成多个URL(换行分隔) */
const generateSearchUrls = (taskType: number, keywords: string): string => {
  if (!keywords) return ''

  // 按换行符分割关键词,过滤空行
  const keywordList = keywords
    .split('\n')
    .map((k) => k.trim())
    .filter((k) => k)

  // 为每个关键词生成URL,再用换行符连接
  return keywordList.map((keyword) => generateSearchUrl(taskType, keyword)).join('\n')
}

/** 处理搜索方式变化 */
const handleSearchTypeChange = (value: number) => {
  if (value === 1) {
    // 切换到关键词模式,清空searchUrl
    formData.value.searchUrl = ''
  } else {
    // 切换到链接模式,清空keyword
    formData.value.keyword = ''
  }
}

const handlePostSourceModeChange = () => {
  formData.value.keyword = ''
  formData.value.searchUrl = ''
  formData.value.searchType = postSourceMode.value === 'search' ? 1 : 0
  selectedPostPages.value = []
  selectedPostGroups.value = []
}

const handlePostPageInputModeChange = (mode: 'manual' | 'select') => {
  formData.value.searchUrl = ''
  if (mode === 'manual') {
    selectedPostPages.value = []
  }
}

const handlePostGroupInputModeChange = (mode: 'manual' | 'select') => {
  formData.value.searchUrl = ''
  if (mode === 'manual') {
    selectedPostGroups.value = []
  }
}

const openPostPageSelector = () => {
  userSelectorVisible.value = true
}

const openPostGroupSelector = () => {
  groupSelectorVisible.value = true
}

const refreshPostPageUrls = () => {
  formData.value.searchUrl = selectedPostPages.value.map((user) => toPagePostsUrl(user.url)).join('\n')
}

const refreshPostGroupUrls = () => {
  formData.value.searchUrl = selectedPostGroups.value.map((group) => toGroupPostsUrl(group.url)).join('\n')
}

const removeSelectedPostPage = (userId: number) => {
  const index = selectedPostPages.value.findIndex((user) => user.id === userId)
  if (index > -1) {
    selectedPostPages.value.splice(index, 1)
    refreshPostPageUrls()
  }
}

const removeSelectedPostGroup = (groupId: number) => {
  const index = selectedPostGroups.value.findIndex((group) => group.id === groupId)
  if (index > -1) {
    selectedPostGroups.value.splice(index, 1)
    refreshPostGroupUrls()
  }
}

/** 打开群组选择弹框 */
const openGroupSelector = () => {
  groupSelectorVisible.value = true
}

/** 处理群组成员采集输入模式切换 */
const handleGroupInputModeChange = (mode: 'manual' | 'select') => {
  if (mode === 'manual') {
    // 切换到手动输入，清空已选群组
    selectedGroups.value = []
    formData.value.searchUrl = ''
  } else {
    // 切换到从群组选择，清空手动输入的链接
    formData.value.searchUrl = ''
  }
}

/** 移除已选择的群组 */
const removeSelectedGroup = (groupId: number) => {
  const index = selectedGroups.value.findIndex((g) => g.id === groupId)
  if (index > -1) {
    selectedGroups.value.splice(index, 1)
    // 重新生成链接
    if (selectedGroups.value.length > 0) {
      const memberUrls = selectedGroups.value.map((group) => {
        const baseUrl = group.url.split('?')[0]
        return `${baseUrl}/members`
      })
      formData.value.searchUrl = memberUrls.join('\n')
    } else {
      formData.value.searchUrl = ''
    }
  }
}

/** 确认群组选择（组件回调） */
const handleGroupConfirm = (groups: FbCollectGroup[]) => {
  if (formData.value.taskType === 2 && postSourceMode.value === 'group') {
    selectedPostGroups.value = groups
    refreshPostGroupUrls()
    message.success(`已选择 ${groups.length} 个群组，生成了 ${groups.length} 个群组帖子采集链接`)
    return
  }

  selectedGroups.value = groups

  // 生成带/members后缀的采集链接
  const memberUrls = groups.map((group) => {
    const baseUrl = group.url.split('?')[0] // 移除查询参数
    return `${baseUrl}/members`
  })

  // 将URLs用换行符连接
  formData.value.searchUrl = memberUrls.join('\n')

  message.success(`已选择 ${groups.length} 个群组，生成了 ${memberUrls.length} 个采集链接`)
}

/** 打开用户选择弹框 */
const openUserSelector = () => {
  userSelectorVisible.value = true
}

/** 处理同行采集输入模式切换 */
const handleUserInputModeChange = (mode: 'manual' | 'select') => {
  if (mode === 'manual') {
    // 切换到手动输入，清空已选用户
    selectedUsers.value = []
    formData.value.searchUrl = ''
  } else {
    // 切换到从潜客选择，清空手动输入的链接
    formData.value.searchUrl = ''
  }
}

/** 移除已选择的用户 */
const removeSelectedUser = (userId: number) => {
  const index = selectedUsers.value.findIndex((u) => u.id === userId)
  if (index > -1) {
    selectedUsers.value.splice(index, 1)
    // 重新生成链接
    if (selectedUsers.value.length > 0) {
      generateUserRelationUrls()
    } else {
      formData.value.searchUrl = ''
    }
  }
}

/** 生成同行采集链接 */
const generateUserRelationUrls = () => {
  const urls: string[] = []

  selectedUsers.value.forEach((user) => {
    // 保留原始URL（包括查询参数）
    const originalUrl = user.url
    // 判断URL是否已有查询参数
    const hasQuery = originalUrl.includes('?')

    // 根据选中的关系类型生成不同的链接
    relationTypes.value.forEach((relationType) => {
      let param = ''
      if (relationType === 'followers') {
        param = 'sk=followers'
      } else if (relationType === 'following') {
        param = 'sk=following'
      } else if (relationType === 'friends') {
        param = 'sk=friends'
      }

      if (param) {
        // 如果已有查询参数用 &，否则用 ?
        const separator = hasQuery ? '&' : '?'
        urls.push(`${originalUrl}${separator}${param}`)
      }
    })
  })

  formData.value.searchUrl = urls.join('\n')
}

/** 确认用户选择（组件回调） */
const handleUserConfirm = (users: FbCollectUser[]) => {
  if (formData.value.taskType === 2 && postSourceMode.value === 'page') {
    selectedPostPages.value = users
    refreshPostPageUrls()
    message.success(`已选择 ${users.length} 个潜客，生成了 ${users.length} 个 Page 帖子采集链接`)
    return
  }

  selectedUsers.value = users

  if (relationTypes.value.length === 0) {
    message.warning('请至少选择一种关系类型')
    return
  }

  // 生成带关系后缀的采集链接
  generateUserRelationUrls()

  const relationNames = {
    followers: '粉丝',
    following: '关注',
    friends: '好友'
  }
  const selectedRelations = relationTypes.value
    .map((t) => relationNames[t as keyof typeof relationNames])
    .join('、')

  message.success(
    `已选择 ${users.length} 个用户，${selectedRelations}，生成了 ${formData.value.searchUrl.split('\n').length} 个采集链接`
  )
}

// 监听keyword和taskType变化,自动生成URL
watch(
  () => [formData.value.keyword, formData.value.taskType],
  ([newKeyword, newTaskType]) => {
    // 只有在关键词模式下才自动生成URL
    if (formData.value.searchType === 1 && newKeyword && newTaskType) {
      formData.value.searchUrl = generateSearchUrls(newTaskType, newKeyword)
    }
  }
)

// 监听同行采集的关系类型变化,重新生成链接
watch(
  () => relationTypes.value,
  () => {
    // 只有在同行采集模式且从潜客选择模式下才生效
    if (
      formData.value.taskType === 8 &&
      userInputMode.value === 'select' &&
      selectedUsers.value.length > 0
    ) {
      generateUserRelationUrls()
    }
  },
  { deep: true }
)
</script>
