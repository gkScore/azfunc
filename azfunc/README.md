# zkScore-API

zkScoreのbackend用API。
記号
```bash
export API_KEY=<api_key>
export API_URL=<api_url>
```

# reputations
## GET
command:
```bash
curl -X GET curl $API_URL/reputations/?code=$API_URL\&evaluatee_id=2
```
where
  * evaluatee_id: 被評価者のID（wallet addressではない）

throws:
  * when evaluatee_id is not found.

returns:
```json
{"evaluatee_id":"2","evaluatee_address":"test5","score":5.4,"score_count":2}
```
where  
  * evaluatee_id: パラメータで指定されたID
  * evaluatee_address: evaluatee_idで指定されたユーザのwallet address（いらないかも？）
  * score: 付与されたスコアの平均
  * score_count: スコアをつけてもらった回数

## POST
command:
```bash
curl -X POST $API_URL/reputations/?code=$API_URL -H "Content-Type:application/json" -d '{"reviewer_address":"abc","evaluatee_address":"test","score":5.3}'
```
where
  * reviewer_address: 評価者のaddress
  * evaluatee_address: 被評価者のaddress
  * score: この評価におけるスコア

throws:
  * when evaluatee_address is not registered.

returns:  
none


# userid
## GET
command:
```bash
curl -X GET $API_URL/userid/?code=$API_URL&wallet_address=test
```
where  
  * wallet_address: user_idを検索する対象のwallet address

throws:
  * when wallet_address is not registered.

returns:
```json
{"user_id":"1","wallet_address":"test"}
```
where  
  * user_id: 検索されたuser_id
  * wallet_address: 検索対象のwallet address

## POST
command:
```bash
curl -X POST $API_URL/userid/?code=$API_URL -H "Content-Type:application/json" -d '{"wallet_address":"test"}'
```
where  
  * wallet_address: 新規登録するwallet address

throws:
  * when wallet_address is already registered.

returns:  
```json
{"user_id":"4","wallet_address":"test"}
```
where  
  * user_id: 新規登録の際に発行されたuser_id
  * wallet_address: 新規登録対象のwallet address
