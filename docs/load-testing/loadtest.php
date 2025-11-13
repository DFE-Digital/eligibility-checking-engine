<?php

use Amp\Http\Client\HttpClientBuilder;
use Amp\Future;
use Amp\Http\Client\Request;

if ($argc < 3) {
    echo "Usage: php script.php <username> <password>\n";
    die();
}

require("vendor/autoload.php");

$root = $argv[3]??"https://eligibility-checking-engine.education.gov.uk";
$user = $argv[1];
$password = $argv[2];
$tests = array(
    "https://www.google.com/generate_204"=>array("body"=>str_repeat("whatever", 10), "token"=>false, "response"=>false),
    "/oauth2/token"=>array("body"=>"client_id=$user&client_secret=$password&scope=bulk_check check free_school_meals local_authority", "token"=>false, "response"=>false),
    "/check/free-school-meals"=>array(),
    "/bulk-check/free-school-meals#1"=>array("file"=>true, "quantity"=>"1"),
    "/bulk-check/free-school-meals#10"=>array("file"=>true, "quantity"=>"10"),
    //"/bulk-check/free-school-meals#100"=>array("file"=>true, "quantity"=>"100"),
    /*"/bulk-check/free-school-meals#1000"=>array("file"=>true, "quantity"=>"1000"),
    "/bulk-check/free-school-meals#5000"=>array("file"=>true, "quantity"=>"5000")*/
);

$size = array(1, 1.1, 5, 10, 20, 50, //100, //200, //1000
);

$http_client = HttpClientBuilder::buildDefault();

$token = "";
$times = array();

echo "URL\t".implode("\tComplete\t", $size)."\tComplete";
foreach($tests as $url=>$settings) {
    echo "\n".$url;
    foreach($size as $quantity) {
        $start = microtime(true);

        $items = range(0, round($quantity)-1);

        $responses = Future\await(
            array_map(
                function() use ($settings, $http_client, $token, $root, $url) {
                    if(@$settings['body']) $data = $settings['body'];
                    elseif(@$settings['file']) {
                        $people = array();

                        foreach(range(0, $settings['quantity']-1) as $i) {
                            $people[] = array(
                                "type"=>"FreeSchoolMeals",
                                "nationalInsuranceNumber"=>"NN".rand(100000, 999999)."C",
                                "lastName"=>"TESTER",
                                "dateOfBirth"=>"2000-01-01",
                                "sequence"=>$i+1
                            );
                        }

                        $data = json_encode(array("data"=>$people));
                    }
                    else {
                        $data = '
                        {
                          "data": {
                            "type": "FreeSchoolMeals",
                            "nationalInsuranceNumber": "NN'.rand(100000, 999999).'C",
                            "lastName": "TESTER",
                            "dateOfBirth": "2000-01-01"
                          }
                        }
                        ';
                    }

                    $request = new Request((strpos($url, "https://")===false?$root:"").$url, 'POST', $data);
                    $request->addHeader('Content-Type', (@$settings['token']!==false?'application/json':'application/x-www-form-urlencoded'));


                    if(@$settings['token']!==false) $request->addHeader('Authorization', 'Bearer '.$token);

                    $request->setTransferTimeout(180);
                    $request->setInactivityTimeout(180);
                    return \Amp\async(
                        function() use($request, $http_client) {
                            try {
                                return $http_client->request($request);
                            } catch (\Throwable $e) {
                                echo "Request failed for URL {$request->getUri()}: " . $e->getMessage() . "\n";
                                throw $e; // rethrow if you want Future\await to fail as well
                            }
                        }
                    );
                },
                $items
            )
        );

        $last = array();

        foreach($responses as $response) {
            $data = $response->getBody()->buffer();

            $response_data = $last = json_decode($data, 1);

            if(@$settings['token']===false&&isset($response_data['access_token'])) $token = $response_data['access_token'];

        }

        $time = microtime(true)-$start;
        $times[$url]["".$quantity] = $time;

        echo "\t".round($time, 2);

        if(@$settings['response']!==false) {
            do {
                $path = $root.(@$last['links']['get_EligibilityCheck']?$last['links']['get_EligibilityCheck']:@$last['links']['get_Progress_Check']);
                $response = file_get_contents(
                    $path,
                    false,
                    stream_context_create([
                        'http' => [
                            'method' => 'GET',
                            'header' => "Authorization: Bearer $token\r\nAccept: application/json\r\n",
                            'ignore_errors' => true
                        ]
                    ])
                );

                $json = json_decode($response, true)['data'];
                if(!$json) echo $response;

                if(!isset($settings['file'])) $answer = $json['status'];
                else $answer = @$json['complete']<@$json['total']?"queuedForProcessing":@$json['complete']."=".@$json['total'];

            }  while($answer=="queuedForProcessing");

            $time = microtime(true)-$start;
            $times[$url]["".$quantity." complete ".$answer] = $time;

            echo "\t".round($time, 2);
        }
        else echo "\t";
    }
}

echo "\n".json_encode($times, JSON_PRETTY_PRINT);