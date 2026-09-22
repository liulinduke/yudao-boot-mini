-- Small, public starting set for Source Registry. All entries require validation before activation.
INSERT INTO ai_search_source_registry
  (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000001,'Europages','europages.com','B2B_DIRECTORY','COMPANY_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE,PAGINATION',80,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='europages.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000002,'Kompass','kompass.com','BUSINESS_DIRECTORY','COMPANY_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE,PAGINATION',75,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='kompass.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000003,'WLW','wlw.de','B2B_DIRECTORY','DISTRIBUTOR_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE,PAGINATION',70,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='wlw.de' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000004,'IndustryStock','industrystock.com','INDUSTRY_DIRECTORY','INDUSTRY_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE',65,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='industrystock.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000005,'Thomasnet','thomasnet.com','B2B_DIRECTORY','COMPANY_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE,PAGINATION',65,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='thomasnet.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000006,'IndiaMART','indiamart.com','MARKETPLACE','DISTRIBUTOR_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE',55,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='indiamart.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000007,'Global Sources','globalsources.com','WHOLESALE_PLATFORM','DISTRIBUTOR_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE',55,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='globalsources.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000008,'TradeIndia','tradeindia.com','WHOLESALE_PLATFORM','DISTRIBUTOR_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE',50,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='tradeindia.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000009,'Yelp','yelp.com','BUSINESS_DIRECTORY','COMPANY_DISCOVERY','SEARCH_ENGINE,CATEGORY_PAGE,LIST_PAGE,DETAIL_PAGE,PAGINATION',45,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='yelp.com' AND tenant_id=1);
INSERT INTO ai_search_source_registry (id,name,domain,source_type,source_role,discovery_methods,priority,status,verified,usage_count,company_count,qualified_company_count,duplicate_count,tenant_id)
SELECT 910000000000000010,'Crunchbase','crunchbase.com','BUSINESS_DIRECTORY','COMPANY_DISCOVERY','SEARCH_ENGINE,LIST_PAGE,DETAIL_PAGE',45,'CANDIDATE',b'0',0,0,0,0,1
WHERE NOT EXISTS (SELECT 1 FROM ai_search_source_registry WHERE domain='crunchbase.com' AND tenant_id=1);
