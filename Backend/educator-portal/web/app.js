"use strict";
const config = window.OSTEON_EDUCATOR_CONFIG;
const $ = id => document.getElementById(id);
let accessToken = null, expiresAt = 0, bank = [], baseVersion = null, selected = null, dirty = false, cursor = null, publicationId = null, programNames = new Map();
const notice = message => { $("notice").textContent = message; };
const encode = bytes => btoa(String.fromCharCode(...bytes)).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/g,"");
function show(view){for(const id of ["welcome","questions","learners"])$(id).hidden=id!==view;}
const editFields=["bone_group","bone_name","difficulty_level","difficulty_score","question_text","option_a","option_b","option_c","option_d","correct_option"];
function editorHasChanges(){return selected!==null&&editFields.some(field=>$("editor").elements[field].value.trim()!==String(bank[selected][field]));}
function leaveDraft(){return (!dirty&&!editorHasChanges()) || confirm("Discard unpublished question changes?");}
function session(){if(!accessToken || Date.now()>=expiresAt){accessToken=null;show("welcome");throw Error("Your educator session expired. Sign in again.");}}
async function api(path, options={}){
  session();
  const response=await fetch(config.apiBase+path,{...options,headers:{Authorization:"Bearer "+accessToken,...(options.body?{"Content-Type":"application/json"}:{})},cache:"no-store"});
  const data=await response.json().catch(()=>({}));
  if(response.status===401||response.status===403){accessToken=null;show("welcome");throw Error("Educator access ended. Sign in again.");}
  if(!response.ok){const error=Error(data.message||data.error||"Request failed");error.status=response.status;throw error;}
  return data;
}
function randomString(){return encode(crypto.getRandomValues(new Uint8Array(32)));}
async function login(endpoint="/oauth2/authorize"){
  const verifier=randomString(),state=randomString(),digest=await crypto.subtle.digest("SHA-256",new TextEncoder().encode(verifier));
  sessionStorage.setItem("osteon-pkce",JSON.stringify({verifier,state}));
  const url=new URL(config.cognitoDomain+endpoint);
  url.search=new URLSearchParams({response_type:"code",client_id:config.clientId,redirect_uri:config.redirectUri,scope:"openid",state,code_challenge:encode(new Uint8Array(digest)),code_challenge_method:"S256"});
  location.assign(url);
}
async function callback(){
  const url=new URL(location.href),code=url.searchParams.get("code");
  if(!code){if(url.searchParams.has("error"))notice("Sign-in was cancelled or failed.");return;}
  const pending=JSON.parse(sessionStorage.getItem("osteon-pkce")||"null");
  sessionStorage.removeItem("osteon-pkce");history.replaceState({},"",config.redirectUri);
  if(!pending||pending.state!==url.searchParams.get("state"))throw Error("Sign-in state did not match. Try again.");
  const response=await fetch(config.cognitoDomain+"/oauth2/token",{method:"POST",headers:{"Content-Type":"application/x-www-form-urlencoded"},body:new URLSearchParams({grant_type:"authorization_code",client_id:config.clientId,code,redirect_uri:config.redirectUri,code_verifier:pending.verifier})});
  const token=await response.json();if(!response.ok||!token.access_token)throw Error("Sign-in could not be completed.");
  accessToken=token.access_token;expiresAt=Date.now()+Math.min(Number(token.expires_in)||900,900)*1000;
  show("questions");await loadQuestions();
}
function renderQuestions(){
  const list=$("question-list");list.replaceChildren();const term=$("question-search").value.trim().toLocaleLowerCase();
  bank.forEach((row,index)=>{if(term&&!(`${row.question_id} ${row.question_text} ${row.bone_name}`.toLocaleLowerCase().includes(term)))return;
    const button=document.createElement("button");button.type="button";button.textContent=`${row.question_id}: ${row.question_text}`;button.setAttribute("aria-current",String(selected===index));button.onclick=()=>selectQuestion(index);list.append(button);});
  $("version").textContent="Published bank: "+baseVersion;
  $("draft-state").textContent=dirty?"Unpublished changes in this browser":"No unpublished changes";
  $("publish").disabled=!dirty;
}
function selectQuestion(index){if(editorHasChanges()&&!confirm("Discard unsaved edits to this question?"))return;selected=index;const row=bank[index],form=$("editor");form.hidden=false;$("editing").textContent=row.question_id;
  for(const field of editFields)form.elements[field].value=row[field];renderQuestions();}
function saveDraft(event){event.preventDefault();if(selected===null)return;const row=bank[selected],form=$("editor");
  for(const field of editFields)row[field]=form.elements[field].value.trim();
  dirty=true;publicationId=null;renderQuestions();notice("Draft saved in this browser. Publish to make it live for new quizzes.");}
async function loadQuestions(){const data=await api("/v1/questions");bank=data.questions;baseVersion=data.version;selected=null;dirty=false;publicationId=null;$("editor").hidden=true;renderQuestions();}
async function publish(){if(!dirty)return;if(editorHasChanges()){notice("Save this question draft before publishing.");return;}publicationId??=crypto.randomUUID();$("publish").disabled=true;
  try{const data=await api("/v1/questions/publish",{method:"POST",body:JSON.stringify({base_version:baseVersion,questions:bank,request_id:publicationId})});notice("Published bank "+data.version);await loadQuestions();}
  catch(error){notice(error.status===409?"Another educator published first. Reload and reapply your edits.":error.message);$("publish").disabled=false;}}
function element(tag,text){const node=document.createElement(tag);node.textContent=String(text??"");return node;}
function metric(label,value){const card=element("div","");card.className="metric";card.append(element("span",label),element("strong",value));return card;}
function breakdown(title,items){
  const section=element("section","");section.className="analysis-block";section.append(element("h4",title));
  if(!items.length){section.append(element("p","No categorized answers in the retained window."));return section;}
  for(const item of items){
    const row=element("div","");row.className="breakdown-row";
    row.append(element("span",`${item.label}: ${item.correct}/${item.answered} correct`));
    const track=element("div","");track.className="bar-track";
    const fill=element("div","");fill.className="bar-fill";fill.style.width=`${100*item.correct/item.answered}%`;
    track.append(fill);row.append(track);section.append(row);
  }
  return section;
}
function masteryChart(estimates){
  const section=element("section","");section.className="analysis-block";section.append(element("h4","Mastery estimate across retained answers"));
  if(estimates.length<2){section.append(element("p","A mastery trace is unavailable for these retained answers."));return section;}
  const delta=Math.round((estimates.at(-1)-estimates[0])*100);
  section.append(element("p",`${Math.round(estimates[0]*100)}% → ${Math.round(estimates.at(-1)*100)}% (${delta>0?"+":""}${delta} percentage points). This is a model estimate, not a grade.`));
  const svg=document.createElementNS("http://www.w3.org/2000/svg","svg");svg.setAttribute("viewBox","0 0 640 150");svg.setAttribute("role","img");svg.setAttribute("aria-label","Mastery estimate after each retained answer");svg.classList.add("mastery-chart");
  const points=estimates.map((value,index)=>`${20+600*index/(estimates.length-1)},${130-110*value}`).join(" ");
  const line=document.createElementNS("http://www.w3.org/2000/svg","polyline");line.setAttribute("points",points);line.setAttribute("fill","none");line.setAttribute("stroke","#075b9c");line.setAttribute("stroke-width","4");svg.append(line);
  estimates.forEach((value,index)=>{const dot=document.createElementNS("http://www.w3.org/2000/svg","circle");dot.setAttribute("cx",String(20+600*index/(estimates.length-1)));dot.setAttribute("cy",String(130-110*value));dot.setAttribute("r","5");dot.setAttribute("fill","#075b9c");svg.append(dot);});
  section.append(svg);return section;
}
async function loadLearners(next=false){if(!next){cursor=null;$("learner-list").replaceChildren();$("learner-detail").hidden=true;}
  if(!programNames.size){const catalog=await api("/v1/programs");programNames=new Map(catalog.programs.map(item=>[item.id,item.name]));}
  const query=new URLSearchParams({q:$("learner-search").value.trim()});if(cursor)query.set("cursor",cursor);
  const data=await api("/v1/learners?"+query);cursor=data.next_cursor;$("more").hidden=!cursor;
  for(const item of data.learners){const p=item.profile,s=item.progress,card=element("div","");card.className="card";
    const button=element("button",[p.first_name,p.middle_name,p.last_name].filter(Boolean).join(" ")||p.profile_label||"Incomplete profile");button.onclick=()=>loadDetail(p.learner_id);card.append(button);
    card.append(element("span",` ${programNames.get(p.program_id)||p.program_id||"Program incomplete"} · Year ${p.year_level||"incomplete"} · ${s?Math.round(s.mastery_estimate*100)+"% model estimate, "+s.answered_count+" answers this run":"No quiz started"}`));$("learner-list").append(card);}
  if(!data.learners.length&&!next)notice("No learners found on this page.");}
async function loadDetail(id){
  const data=await api("/v1/learners/"+id),p=data.profile,s=data.progress,a=data.analysis,article=$("learner-detail");
  article.replaceChildren();article.hidden=false;
  article.append(element("h3",[p.first_name,p.middle_name,p.last_name].filter(Boolean).join(" ")||"Incomplete profile"));
  const dl=document.createElement("dl");
  for(const [key,value] of [["Learner ID",p.learner_id],["Program",programNames.get(p.program_id)||p.program_id||"Incomplete"],["Year",p.year_level||"Incomplete"],["Quiz status",s?(s.completed?"Completed":"In progress"):"Not started"],["Current question",s?.current_question_id||"None"]])dl.append(element("dt",key),element("dd",value));
  article.append(dl);
  const metrics=element("div","");metrics.className="metrics";
  metrics.append(metric("Mastery estimate",s?Math.round(s.mastery_estimate*100)+"%":"No quiz"),
    metric("Answers this run",s?.answered_count??0),
    metric("Retained answers",a.retained_count),
    metric("Correct in retained window",a.graded_count?`${a.correct_count}/${a.graded_count} (${Math.round(100*a.correct_count/a.graded_count)}%)`:"No graded answers"));
  article.append(metrics,element("p","Analysis below uses only the most recent retained answers (up to 10). It may include earlier quiz runs. It is not a lifetime score."));
  article.append(masteryChart(a.mastery_estimates),breakdown("By bone group",a.by_group),breakdown("By difficulty",a.by_difficulty));
  article.append(element("h4","Recent answers"));
  if(!data.recent_answers.length)article.append(element("p","No answers retained."));
  for(const answer of [...data.recent_answers].reverse()){
    const row=element("div","");row.className="answer-row";
    row.append(element("strong",answer.question_text||answer.question_id||"Question unavailable"));
    const descriptors=[answer.bone_name,answer.bone_group,answer.difficulty].filter(Boolean);
    if(answer.accepted_at&&Number.isFinite(Number(answer.accepted_at)))descriptors.push(new Date(Number(answer.accepted_at)*1000).toLocaleString());
    if(descriptors.length)row.append(element("p",descriptors.join(" · ")));
    const chosen=`Choice ${answer.choice||"unknown"}${answer.chosen_text?" — "+answer.chosen_text:""}`;
    const outcome=answer.correct===true?"Correct":answer.correct===false?"Incorrect":"Outcome unavailable";
    row.append(element("p",`${chosen} · ${outcome}`));
    if(answer.correct===false&&answer.correct_option)row.append(element("p",`Correct answer: ${answer.correct_option}${answer.correct_text?" — "+answer.correct_text:""}`));
    if(typeof answer.previous_mastery==="number"&&typeof answer.updated_mastery==="number")row.append(element("p",`Model estimate: ${Math.round(answer.previous_mastery*100)}% → ${Math.round(answer.updated_mastery*100)}%`));
    if(!answer.question_text)row.append(element("p","Original question text unavailable for this legacy answer."));
    article.append(row);
  }
}
window.addEventListener("beforeunload",event=>{if(dirty||editorHasChanges()){event.preventDefault();event.returnValue="";}});
$("login").onclick=()=>login().catch(error=>notice(error.message));
$("signup").onclick=()=>login("/signup").catch(error=>notice(error.message));
$("logout").onclick=()=>{if(!leaveDraft())return;accessToken=null;expiresAt=0;dirty=false;sessionStorage.removeItem("osteon-pkce");const url=new URL(config.cognitoDomain+"/logout");url.search=new URLSearchParams({client_id:config.clientId,logout_uri:config.redirectUri});location.assign(url);};
$("questions-tab").onclick=()=>{if(!leaveDraft())return;show(accessToken?"questions":"welcome");if(accessToken)loadQuestions().catch(error=>notice(error.message));};
$("learners-tab").onclick=()=>{if(!leaveDraft())return;dirty=false;show(accessToken?"learners":"welcome");if(accessToken)loadLearners().catch(error=>notice(error.message));};
$("editor").onsubmit=saveDraft;$("question-search").oninput=renderQuestions;
$("publish").onclick=()=>publish().catch(error=>notice(error.message));
$("reload").onclick=()=>{if(leaveDraft())loadQuestions().catch(error=>notice(error.message));};
$("learner-find").onclick=()=>loadLearners().catch(error=>notice(error.message));
$("more").onclick=()=>loadLearners(true).catch(error=>notice(error.message));
if(config?.apiBase&&config?.cognitoDomain&&config?.clientId&&config?.redirectUri)callback().catch(error=>notice(error.message));else notice("Portal configuration is missing.");
